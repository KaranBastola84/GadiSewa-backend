using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.DTOs.Staff;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "BackOfficeOnly")]
public sealed class StaffController : ControllerBase
{
    private readonly ILogger<StaffController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public StaffController(
        IUserRepository userRepository,
        IRepository<Staff> staffRepository,
        IRepository<RefreshToken> refreshTokenRepository,
        IPasswordHasherService passwordHasherService,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ILogger<StaffController> logger)
    {
        _userRepository = userRepository;
        _staffRepository = staffRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasherService = passwordHasherService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all staff members",
        Description = "Retrieves a list of all staff members in the system.")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StaffDto>>>> GetAllStaff(CancellationToken cancellationToken)
    {
        var staffMembers = await _staffRepository.Query()
            .AsNoTracking()
            .Include(s => s.User)
            .OrderBy(s => s.User.LastName)
            .ThenBy(s => s.User.FirstName)
            .ToListAsync(cancellationToken);

        var result = staffMembers.Select(StaffDto.FromStaff).ToList();
        return Ok(ApiResponse<IReadOnlyList<StaffDto>>.Success(result));
    }

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get staff member by ID",
        Description = "Retrieves a specific staff member by their ID.")]
    [ProducesResponseType(typeof(ApiResponse<StaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StaffDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StaffDto>>> GetStaffById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var staff = await _staffRepository.Query()
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (staff is null)
        {
            return NotFound(ApiResponse<StaffDto>.Failure("Staff member not found.", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<StaffDto>.Success(StaffDto.FromStaff(staff)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [SwaggerOperation(
        Summary = "Update staff member details",
        Description = "Updates the details of a specific staff member.")]
    [ProducesResponseType(typeof(ApiResponse<StaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StaffDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StaffDto>>> UpdateStaff(
        Guid id,
        [FromBody] UpdateStaffRequestDto request,
        CancellationToken cancellationToken)
    {
        var staff = await _staffRepository.Query()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (staff is null)
        {
            return NotFound(ApiResponse<StaffDto>.Failure("Staff member not found.", StatusCodes.Status404NotFound));
        }

        // Update User details
        staff.User.FirstName = request.FirstName.Trim();
        staff.User.LastName = request.LastName.Trim();
        staff.User.PhoneNumber = request.PhoneNumber.Trim();
        staff.User.UpdatedAt = DateTimeOffset.UtcNow;

        // Update Staff details
        staff.Position = request.Position.Trim();
        staff.HireDate = request.HireDate;
        staff.IsAvailable = request.IsAvailable;

        _staffRepository.Update(staff);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<StaffDto>.Success(StaffDto.FromStaff(staff)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [SwaggerOperation(
        Summary = "Deactivate/delete a staff member",
        Description = "Deactivates a staff member account by setting IsActive to false. This is a soft delete.")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteStaff(
        Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        // Prevent staff from deactivating their own account
        if (id == currentUserId)
        {
            return BadRequest(ApiResponse<object?>.Failure(
                "You cannot deactivate your own account.",
                StatusCodes.Status400BadRequest));
        }

        var staff = await _staffRepository.Query()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (staff is null)
        {
            return NotFound(ApiResponse<object?>.Failure("Staff member not found.", StatusCodes.Status404NotFound));
        }

        // Soft delete by setting IsActive to false
        staff.User.IsActive = false;
        staff.User.UpdatedAt = DateTimeOffset.UtcNow;
        _staffRepository.Update(staff);

        // Revoke all refresh tokens
        var refreshTokens = await _refreshTokenRepository.ListAsync(
            x => x.UserId == staff.UserId && x.RevokedAt == null,
            cancellationToken);

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.RevokedAt = DateTimeOffset.UtcNow;
            _refreshTokenRepository.Update(refreshToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object?>.Success(null));
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
}
