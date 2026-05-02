using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Customers;

public sealed class UpdateCustomerRequestDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [MaxLength(20)]
    public string PhoneNumber { get; init; } = string.Empty;

    [MaxLength(300)]
    public string Address { get; init; } = string.Empty;
}