using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Customers;

public sealed class CreateVehicleRequestDto
{
    [Required]
    [MaxLength(30)]
    public string RegistrationNumber { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Make { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Model { get; init; } = string.Empty;

    [Range(1900, 2100)]
    public int Year { get; init; }

    [Range(0, int.MaxValue)]
    public int Mileage { get; init; }

    [MaxLength(50)]
    public string Color { get; init; } = string.Empty;
}