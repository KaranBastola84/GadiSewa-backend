using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class ResetPasswordRequestDto
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}