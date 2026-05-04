using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Staff;

public sealed class UpdateStaffRequestDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [MaxLength(20)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Position { get; init; } = string.Empty;

    [Required]
    public DateOnly HireDate { get; init; }

    [Required]
    public bool IsAvailable { get; init; }
}
