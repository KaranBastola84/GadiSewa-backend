using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class VerifyEmailRequestDto
{
    [Required]
    public string Token { get; init; } = string.Empty;
}