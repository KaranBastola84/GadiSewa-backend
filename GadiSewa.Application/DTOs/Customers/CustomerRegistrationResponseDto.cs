using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.DTOs.Customers;

public sealed class CustomerRegistrationResponseDto
{
    public Guid CustomerId { get; init; }

    public UserProfileDto User { get; init; } = new();

    public string Address { get; init; } = string.Empty;

    public IReadOnlyList<VehicleDto> Vehicles { get; init; } = Array.Empty<VehicleDto>();

    public static CustomerRegistrationResponseDto FromEntities(User user, Customer customer, IReadOnlyList<Vehicle> vehicles)
    {
        return new CustomerRegistrationResponseDto
        {
            CustomerId = customer.Id,
            User = UserProfileDto.FromUser(user),
            Address = customer.Address,
            Vehicles = vehicles.Select(VehicleDto.FromVehicle).ToList()
        };
    }
}