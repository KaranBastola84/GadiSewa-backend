using GadiSewa.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class RegisterRequestDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(20)]
    public string PhoneNumber { get; init; } = string.Empty;

    public UserRole Role { get; init; } = UserRole.Customer;
}
