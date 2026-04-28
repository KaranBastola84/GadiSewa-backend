using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, cancellationToken);
            return Ok(ApiResponse<AuthResponseDto>.Success(response));
        }
        catch (ConflictException ex)
        {
            return Conflict(ApiResponse<AuthResponseDto>.Failure(ex.Message, StatusCodes.Status409Conflict));
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            return Ok(ApiResponse<AuthResponseDto>.Success(response));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Failure(ex.Message, StatusCodes.Status401Unauthorized));
        }
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<ActionResult<ApiResponse<object?>>> VerifyEmail(
        [FromBody] VerifyEmailRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _authService.VerifyEmailAsync(request, cancellationToken);
            return Ok(ApiResponse<object?>.Success(null));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponse<object?>.Failure(ex.Message, StatusCodes.Status401Unauthorized));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Failure(ex.Message, StatusCodes.Status404NotFound));
        }
    }

    [AllowAnonymous]
    [HttpPost("resend-verification")]
    public async Task<ActionResult<ApiResponse<object?>>> ResendVerification(
        [FromBody] ResendVerificationRequestDto request,
        CancellationToken cancellationToken)
    {
        await _authService.ResendVerificationEmailAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Success(new
        {
            message = "If the email exists, a verification email has been sent."
        }));
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Profile(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var profile = await _authService.GetProfileAsync(userId, cancellationToken);
            return Ok(ApiResponse<UserProfileDto>.Success(profile));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponse<UserProfileDto>.Failure(ex.Message, StatusCodes.Status401Unauthorized));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<UserProfileDto>.Failure(ex.Message, StatusCodes.Status404NotFound));
        }
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile(
        [FromBody] UpdateProfileRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var profile = await _authService.UpdateProfileAsync(userId, request, cancellationToken);
            return Ok(ApiResponse<UserProfileDto>.Success(profile));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponse<UserProfileDto>.Failure(ex.Message, StatusCodes.Status401Unauthorized));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<UserProfileDto>.Failure(ex.Message, StatusCodes.Status404NotFound));
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object?>>> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _authService.ChangePasswordAsync(userId, request, cancellationToken);
            return Ok(ApiResponse<object?>.Success(null));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponse<object?>.Failure(ex.Message, StatusCodes.Status401Unauthorized));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Failure(ex.Message, StatusCodes.Status404NotFound));
        }
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<object?>>> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        await _authService.RequestPasswordResetAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Success(new
        {
            message = "If the email exists, a password reset token has been sent."
        }));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<object?>>> ResetPassword(
        [FromBody] ResetPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _authService.ResetPasswordAsync(request, cancellationToken);
            return Ok(ApiResponse<object?>.Success(null));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponse<object?>.Failure(ex.Message, StatusCodes.Status401Unauthorized));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Failure(ex.Message, StatusCodes.Status404NotFound));
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken(
        [FromBody] RefreshTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request, cancellationToken);
            return Ok(ApiResponse<AuthResponseDto>.Success(response));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Failure(ex.Message, StatusCodes.Status401Unauthorized));
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object?>>> Logout(
        [FromBody] RefreshTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Success(null));
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedException("Invalid user identity.");
        }

        return userId;
    }
}
