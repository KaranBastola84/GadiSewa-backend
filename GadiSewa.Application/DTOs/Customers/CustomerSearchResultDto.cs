namespace GadiSewa.Application.DTOs.Customers;

public sealed class CustomerSearchResultDto
{
    public Guid CustomerId { get; init; }

    public Guid UserId { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public int LoyaltyPoints { get; init; }

    public int VehicleCount { get; init; }

    public int AppointmentCount { get; init; }

    public int ReviewCount { get; init; }

    public bool IsActive { get; init; }

    public List<CustomerVehicleDto> Vehicles { get; init; } = [];
}

public sealed class CustomerVehicleDto
{
    public Guid VehicleId { get; init; }

    public string RegistrationNumber { get; init; } = string.Empty;

    public string Make { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int Year { get; init; }

    public string Color { get; init; } = string.Empty;
}
