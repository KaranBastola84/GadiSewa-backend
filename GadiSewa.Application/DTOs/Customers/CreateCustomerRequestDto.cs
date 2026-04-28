using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Customers;

public sealed class CreateCustomerRequestDto
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

    [MaxLength(300)]
    public string Address { get; init; } = string.Empty;

    [Required]
    public List<CreateVehicleRequestDto> Vehicles { get; init; } = new();
}