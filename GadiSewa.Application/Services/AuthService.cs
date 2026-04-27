using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace GadiSewa.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRepository<PasswordResetToken> _passwordResetTokenRepository;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailService _emailService;

    public AuthService(
        IUserRepository userRepository,
        IRepository<PasswordResetToken> passwordResetTokenRepository,
        IRepository<RefreshToken> refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasherService passwordHasherService,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasherService = passwordHasherService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailService = emailService;
    }

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
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
            Role = UserRole.Customer,
            IsActive = true
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendWelcomeEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user, cancellationToken);
        return AuthResponseDto.FromUser(user, token, refreshToken);
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !_passwordHasherService.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("User account is not active.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user, cancellationToken);
        return AuthResponseDto.FromUser(user, token, refreshToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken.Trim());
        var now = DateTimeOffset.UtcNow;

        var refreshTokens = await _refreshTokenRepository.ListAsync(
            x => x.TokenHash == tokenHash && x.RevokedAt == null,
            cancellationToken);

        var refreshToken = refreshTokens.FirstOrDefault(x => x.ExpiresAt > now);
        if (refreshToken is null)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        var user = await _userRepository.GetByIdAsync(refreshToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("User account is not active.");
        }

        var newRefreshToken = await CreateRefreshTokenAsync(user, cancellationToken);
        refreshToken.RevokedAt = now;
        refreshToken.ReplacedByTokenHash = HashToken(newRefreshToken);

        _refreshTokenRepository.Update(refreshToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenGenerator.GenerateToken(user);
        return AuthResponseDto.FromUser(user, accessToken, newRefreshToken);
    }

    public async Task LogoutAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken.Trim());
        var refreshTokens = await _refreshTokenRepository.ListAsync(
            x => x.TokenHash == tokenHash && x.RevokedAt == null,
            cancellationToken);

        var refreshToken = refreshTokens.FirstOrDefault();
        if (refreshToken is null)
        {
            return;
        }

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        _refreshTokenRepository.Update(refreshToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        return UserProfileDto.FromUser(user);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UserProfileDto.FromUser(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!_passwordHasherService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedException("Current password is incorrect.");
        }

        user.PasswordHash = _passwordHasherService.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestPasswordResetAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return;
        }

        var existingTokens = await _passwordResetTokenRepository.ListAsync(x => x.UserId == user.Id && x.UsedAt == null, cancellationToken);
        foreach (var existingToken in existingTokens)
        {
            _passwordResetTokenRepository.Remove(existingToken);
        }

        var resetToken = GenerateResetToken();
        var resetTokenEntity = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(resetToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _passwordResetTokenRepository.AddAsync(resetTokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendPasswordResetEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), resetToken, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.Token.Trim());
        var now = DateTimeOffset.UtcNow;

        var resetTokens = await _passwordResetTokenRepository.ListAsync(
            x => x.TokenHash == tokenHash && x.UsedAt == null,
            cancellationToken);

        var resetToken = resetTokens.FirstOrDefault(x => x.ExpiresAt > now);

        if (resetToken is null)
        {
            throw new UnauthorizedException("Invalid or expired reset token.");
        }

        var user = await _userRepository.GetByIdAsync(resetToken.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        user.PasswordHash = _passwordHasherService.HashPassword(request.NewPassword);
        user.UpdatedAt = now;
        resetToken.UsedAt = now;

        _userRepository.Update(user);
        _passwordResetTokenRepository.Update(resetToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateResetToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private async Task<string> CreateRefreshTokenAsync(User user, CancellationToken cancellationToken)
    {
        var refreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await _refreshTokenRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return refreshToken;
    }
}
