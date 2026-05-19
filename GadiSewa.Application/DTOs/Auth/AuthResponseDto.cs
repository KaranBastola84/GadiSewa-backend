using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class AuthResponseDto
{
    public Guid UserId { get; init; }

    public Guid? CustomerId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public UserRole Role { get; init; }

    public string Token { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public bool RequiresEmailVerification { get; init; }

    public static AuthResponseDto FromUser(User user, string token, string refreshToken, bool requiresEmailVerification = false, Guid? customerId = null)
    {
        return new AuthResponseDto
        {
            UserId = user.Id,
            CustomerId = customerId,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            Role = user.Role,
            Token = token,
            RefreshToken = refreshToken,
            RequiresEmailVerification = requiresEmailVerification
        };
    }
}
