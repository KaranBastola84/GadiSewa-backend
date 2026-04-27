using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class ResendVerificationRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}