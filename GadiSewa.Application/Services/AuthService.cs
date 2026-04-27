using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailService _emailService;

    public AuthService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasherService passwordHasherService,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailService emailService)
    {
        _userRepository = userRepository;
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
            Role = request.Role,
            IsActive = true
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendWelcomeEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        return AuthResponseDto.FromUser(user, token);
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
        return AuthResponseDto.FromUser(user, token);
    }
}
