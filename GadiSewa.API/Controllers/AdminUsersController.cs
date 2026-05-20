using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly ILogger<AdminUsersController> _logger;
    private const string BootstrapKeyHeaderName = "X-Admin-Bootstrap-Key";

    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminUsersController(
        IUserRepository userRepository,
        IRepository<Staff> staffRepository,
        IRepository<RefreshToken> refreshTokenRepository,
        IPasswordHasherService passwordHasherService,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<AdminUsersController> logger)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _staffRepository = staffRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasherService = passwordHasherService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("bootstrap-admin")]
    [SwaggerOperation(
        Summary = "Bootstrap the first admin account",
        Description = "Use this only once when the system has no admin users yet. Send the X-Admin-Bootstrap-Key header with the secret configured in appsettings, and this endpoint will create a full-admin account.")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> BootstrapAdmin(
        [FromBody] CreateAdminRequestDto request,
        [FromHeader(Name = BootstrapKeyHeaderName)] string bootstrapKey,
        CancellationToken cancellationToken)
    {
        if (!HasValidBootstrapKey(bootstrapKey))
        {
            return Unauthorized(ApiResponse<UserProfileDto>.Failure("Invalid admin bootstrap key.", StatusCodes.Status401Unauthorized));
        }

        var existingAdmins = await _userRepository.ListAsync(x => x.Role == UserRole.Admin, cancellationToken);
        if (existingAdmins.Count > 0)
        {
            return Conflict(ApiResponse<UserProfileDto>.Failure("An admin account already exists.", StatusCodes.Status409Conflict));
        }

        var user = await CreateAdminUserAsync(request, cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Success(UserProfileDto.FromUser(user)));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserProfileDto>>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _userRepository.ListAsync(cancellationToken: cancellationToken);
        var result = users.Select(u => UserProfileDto.FromUser(u, null)).ToList();
        return Ok(ApiResponse<IReadOnlyList<UserProfileDto>>.Success(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponse<UserProfileDto>.Failure("User not found.", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<UserProfileDto>.Success(UserProfileDto.FromUser(user)));
    }

    [HttpPost("staff")]
    [SwaggerOperation(
        Summary = "Create a staff account",
        Description = "Creates a staff account under an existing admin session. EmployeeCode is auto-generated if not provided.")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> CreateStaff(
        [FromBody] CreateStaffRequestDto request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw new ConflictException("A user with this email already exists.");
        }

        // Auto-generate EmployeeCode if not provided or empty
        var employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode)
            ? await GenerateUniqueEmployeeCodeAsync(cancellationToken)
            : request.EmployeeCode.Trim();

        var existingStaff = await _staffRepository.ListAsync(x => x.EmployeeCode == employeeCode, cancellationToken);
        if (existingStaff.Count > 0)
        {
            throw new ConflictException("A staff member with this employee code already exists.");
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasherService.HashPassword(request.Password),
            Role = UserRole.Staff,
            IsActive = true,
            IsEmailVerified = true,
            EmailVerifiedAt = DateTimeOffset.UtcNow
        };

        var staff = new Staff
        {
            UserId = user.Id,
            EmployeeCode = employeeCode,
            Position = request.Position.Trim(),
            HireDate = request.HireDate,
            IsAvailable = true
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _staffRepository.AddAsync(staff, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendWelcomeEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);

        return Ok(ApiResponse<UserProfileDto>.Success(UserProfileDto.FromUser(user)));
    }

    [HttpPost("admin")]
    [SwaggerOperation(
        Summary = "Create an admin account",
        Description = "Creates an admin account after you are already signed in as an admin. This endpoint is for ongoing admin management after the first bootstrap account exists.")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> CreateAdmin(
        [FromBody] CreateAdminRequestDto request,
        CancellationToken cancellationToken)
    {
        var user = await CreateAdminUserAsync(request, cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Success(UserProfileDto.FromUser(user)));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<object?>>> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        // Prevent admin from deactivating their own account
        if (id == currentUserId)
        {
            return BadRequest(ApiResponse<object?>.Failure(
                "You cannot deactivate your own account.",
                StatusCodes.Status400BadRequest));
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponse<object?>.Failure("User not found.", StatusCodes.Status404NotFound));
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        _userRepository.Update(user);

        if (!request.IsActive)
        {
            var refreshTokens = await _refreshTokenRepository.ListAsync(x => x.UserId == id && x.RevokedAt == null, cancellationToken);
            foreach (var refreshToken in refreshTokens)
            {
                refreshToken.RevokedAt = DateTimeOffset.UtcNow;
                _refreshTokenRepository.Update(refreshToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object?>.Success(null));
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<ApiResponse<object?>>> UpdateRole(
        Guid id,
        [FromBody] UpdateUserRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(UserRole), request.Role) || request.Role == 0)
        {
            return BadRequest(ApiResponse<object?>.Failure(
                "Invalid role value.",
                StatusCodes.Status400BadRequest));
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponse<object?>.Failure("User not found.", StatusCodes.Status404NotFound));
        }

        if (user.Role == request.Role)
        {
            return BadRequest(ApiResponse<object?>.Failure(
                "User already has this role.",
                StatusCodes.Status400BadRequest));
        }

        user.Role = request.Role;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        _userRepository.Update(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object?>.Success(null));
    }

    private bool HasValidBootstrapKey(string? bootstrapKey)
    {
        var configuredKey = _configuration["AdminBootstrap:SetupKey"];
        return !string.IsNullOrWhiteSpace(configuredKey)
            && !string.IsNullOrWhiteSpace(bootstrapKey)
            && string.Equals(bootstrapKey, configuredKey, StringComparison.Ordinal);
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedException("Invalid user identity.");
        }

        return userId;
    }

    private async Task<string> GenerateUniqueEmployeeCodeAsync(CancellationToken cancellationToken)
    {
        // Format: EMP + timestamp + random suffix
        // Example: EMP20260502001, EMP20260502002, etc.
        var datePrefix = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var allStaffCodes = await _staffRepository.ListAsync(cancellationToken: cancellationToken);

        var codesWithPrefix = allStaffCodes
            .Select(s => s.EmployeeCode)
            .Where(c => c.StartsWith($"EMP{datePrefix}"))
            .ToList();

        var nextNumber = codesWithPrefix.Count + 1;
        var employeeCode = $"EMP{datePrefix}{nextNumber:D3}";

        // Ensure uniqueness (backup check)
        while (allStaffCodes.Any(s => s.EmployeeCode == employeeCode))
        {
            nextNumber++;
            employeeCode = $"EMP{datePrefix}{nextNumber:D3}";
        }

        return employeeCode;
    }

    private async Task<User> CreateAdminUserAsync(CreateAdminRequestDto request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasherService.HashPassword(request.Password),
            Role = UserRole.Admin,
            IsActive = true,
            IsEmailVerified = true,
            EmailVerifiedAt = DateTimeOffset.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendWelcomeEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);

        return user;
    }
}