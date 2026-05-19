namespace GadiSewa.Application.DTOs.Customers;

public sealed class CustomerFullProfileDto
{
    public CustomerInfoDto CustomerInfo { get; init; } = new();

    public List<VehicleDto> Vehicles { get; init; } = [];

    public List<RecentInvoiceDto> RecentInvoices { get; init; } = [];

    public List<RecentAppointmentDto> RecentAppointments { get; init; } = [];
}

public sealed class CustomerInfoDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public int LoyaltyPoints { get; init; }

    public decimal TotalSpent { get; init; }
}

public sealed class RecentInvoiceDto
{
    public Guid Id { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public DateTimeOffset InvoiceDate { get; init; }

    public decimal TotalAmount { get; init; }

    public string Status { get; init; } = string.Empty;
}

public sealed class RecentAppointmentDto
{
    public Guid Id { get; init; }

    public string AppointmentNumber { get; init; } = string.Empty;

    public DateTimeOffset ScheduledAt { get; init; }

    public string Status { get; init; } = string.Empty;

    public string VehicleRegistration { get; init; } = string.Empty;
}
