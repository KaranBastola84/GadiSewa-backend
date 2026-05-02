namespace GadiSewa.Application.DTOs.Customers;

public sealed class CustomerHistorySummaryDto
{
    public Guid CustomerId { get; init; }

    public List<AppointmentHistoryItemDto> Appointments { get; init; } = new();

    public List<SalesInvoiceHistoryItemDto> Invoices { get; init; } = new();
}

public sealed class AppointmentHistoryItemDto
{
    public Guid AppointmentId { get; init; }
    public string AppointmentNumber { get; init; } = string.Empty;
    public DateTimeOffset ScheduledAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string ProblemDescription { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string VehicleRegistration { get; init; } = string.Empty;
    public string AssignedStaffName { get; init; } = string.Empty;
}

public sealed class SalesInvoiceHistoryItemDto
{
    public Guid InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public DateTimeOffset InvoiceDate { get; init; }
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string CreatedByStaffName { get; init; } = string.Empty;
}