using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class AuthResponseDto
{
    public Guid UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public UserRole Role { get; init; }

    public string Token { get; init; } = string.Empty;

    public static AuthResponseDto FromUser(User user, string token)
    {
        return new AuthResponseDto
        {
            UserId = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            Role = user.Role,
            Token = token
        };
    }
}
