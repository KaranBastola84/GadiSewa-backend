using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.DTOs.Customers;

public sealed class VehicleDto
{
    public Guid VehicleId { get; init; }

    public string RegistrationNumber { get; init; } = string.Empty;

    public string Make { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int Year { get; init; }

    public int Mileage { get; init; }

    public string Color { get; init; } = string.Empty;

    public static VehicleDto FromVehicle(Vehicle vehicle)
    {
        return new VehicleDto
        {
            VehicleId = vehicle.Id,
            RegistrationNumber = vehicle.RegistrationNumber,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            Mileage = vehicle.Mileage,
            Color = vehicle.Color
        };
    }
}