using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace GadiSewa.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<EmailVerificationToken> _emailVerificationTokenRepository;
    private readonly IRepository<PasswordResetToken> _passwordResetTokenRepository;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailService _emailService;

    public AuthService(
        IUserRepository userRepository,
        IRepository<Customer> customerRepository,
        IRepository<EmailVerificationToken> emailVerificationTokenRepository,
        IRepository<PasswordResetToken> passwordResetTokenRepository,
        IRepository<RefreshToken> refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasherService passwordHasherService,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _customerRepository = customerRepository;
        _emailVerificationTokenRepository = emailVerificationTokenRepository;
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
            IsActive = true,
            IsEmailVerified = false
        };

        await _userRepository.AddAsync(user, cancellationToken);
        var verificationToken = await CreateEmailVerificationTokenAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendEmailVerificationEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), verificationToken, cancellationToken);

        var createdCustomerId = await GetCustomerIdByUserIdAsync(user.Id, cancellationToken);

        return AuthResponseDto.FromUser(user, string.Empty, string.Empty, requiresEmailVerification: true, customerId: createdCustomerId == Guid.Empty ? null : createdCustomerId);
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

        if (!user.IsEmailVerified)
        {
            throw new UnauthorizedException("Email is not verified. Please verify your email first.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user, cancellationToken);

        var customerId = await GetCustomerIdByUserIdAsync(user.Id, cancellationToken);

        return AuthResponseDto.FromUser(user, token, refreshToken, customerId: customerId == Guid.Empty ? null : customerId);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.Token.Trim());
        var now = DateTimeOffset.UtcNow;

        var verificationTokens = await _emailVerificationTokenRepository.ListAsync(
            x => x.TokenHash == tokenHash && x.UsedAt == null,
            cancellationToken);

        var verificationToken = verificationTokens.FirstOrDefault(x => x.ExpiresAt > now);
        if (verificationToken is null)
        {
            throw new UnauthorizedException("Invalid or expired verification token.");
        }

        var user = await _userRepository.GetByIdAsync(verificationToken.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
            user.EmailVerifiedAt = now;
            user.UpdatedAt = now;
            _userRepository.Update(user);
        }

        verificationToken.UsedAt = now;
        _emailVerificationTokenRepository.Update(verificationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendWelcomeEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);
    }

    public async Task ResendVerificationEmailAsync(ResendVerificationRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || user.IsEmailVerified)
        {
            return;
        }

        var existingTokens = await _emailVerificationTokenRepository.ListAsync(x => x.UserId == user.Id && x.UsedAt == null, cancellationToken);
        foreach (var existingToken in existingTokens)
        {
            _emailVerificationTokenRepository.Remove(existingToken);
        }

        var verificationToken = await CreateEmailVerificationTokenAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendEmailVerificationEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), verificationToken, cancellationToken);
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

        var customerId = await GetCustomerIdByUserIdAsync(user.Id, cancellationToken);

        return AuthResponseDto.FromUser(user, accessToken, newRefreshToken, customerId: customerId == Guid.Empty ? null : customerId);
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

        Guid? customerId = null;
        if (user.Role == UserRole.Customer)
        {
            var customer = await _customerRepository.Query()
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            customerId = customer?.Id;
        }

        return UserProfileDto.FromUser(user, customerId);
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

        Guid? customerId = null;
        if (user.Role == UserRole.Customer)
        {
            var customer = await _customerRepository.Query()
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            customerId = customer?.Id;
        }

        return UserProfileDto.FromUser(user, customerId);
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

    private async Task<string> CreateEmailVerificationTokenAsync(User user, CancellationToken cancellationToken)
    {
        var verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var entity = new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = HashToken(verificationToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        await _emailVerificationTokenRepository.AddAsync(entity, cancellationToken);
        return verificationToken;
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

    private async Task<Guid> GetCustomerIdByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.ListAsync(c => c.UserId == userId, cancellationToken);
        var customer = customers.FirstOrDefault();
        if (customer is not null)
        {
            return customer.Id;
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is not null && user.Role == UserRole.Customer)
        {
            var newCustomer = new Customer
            {
                UserId = userId,
                Address = string.Empty,
                LoyaltyPoints = 0,
                TotalSpent = 0
            };
            await _customerRepository.AddAsync(newCustomer, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return newCustomer.Id;
        }

        return Guid.Empty;
    }
}
