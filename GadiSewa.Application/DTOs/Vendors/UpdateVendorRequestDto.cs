using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Vendors;

public sealed class UpdateVendorRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContactPerson { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; init; } = string.Empty;

    [MaxLength(300)]
    public string Address { get; init; } = string.Empty;
}
