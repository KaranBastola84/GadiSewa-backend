using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminUsersController : ControllerBase
{
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
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _staffRepository = staffRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasherService = passwordHasherService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserProfileDto>>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _userRepository.ListAsync(cancellationToken: cancellationToken);
        var result = users.Select(UserProfileDto.FromUser).ToList();
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

        var existingStaff = await _staffRepository.ListAsync(x => x.EmployeeCode == request.EmployeeCode.Trim(), cancellationToken);
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
            EmployeeCode = request.EmployeeCode.Trim(),
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

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<object?>>> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequestDto request,
        CancellationToken cancellationToken)
    {
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
}