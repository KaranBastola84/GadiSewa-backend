namespace GadiSewa.Application.DTOs.Customers;

public sealed class CustomerHistorySummaryDto
{
    public Guid CustomerId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public int TotalAppointments { get; init; }

    public int CompletedAppointments { get; init; }

    public int CancelledAppointments { get; init; }

    public int TotalInvoices { get; init; }

    public decimal TotalSpent { get; init; }

    public decimal TotalUnpaid { get; init; }

    public decimal TotalLoyaltyPoints { get; init; }

    public DateTimeOffset? FirstAppointmentDate { get; init; }

    public DateTimeOffset? LastAppointmentDate { get; init; }

    public DateTimeOffset? FirstPurchaseDate { get; init; }

    public DateTimeOffset? LastPurchaseDate { get; init; }

    public List<AppointmentHistoryItemDto> RecentAppointments { get; init; } = [];

    public List<SalesInvoiceHistoryItemDto> RecentInvoices { get; init; } = [];
}

public sealed class AppointmentHistoryItemDto
{
    public Guid AppointmentId { get; init; }

    public string AppointmentNumber { get; init; } = string.Empty;

    public string VehicleRegistration { get; init; } = string.Empty;

    public DateTimeOffset ScheduledAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string Status { get; init; } = string.Empty;

    public string ProblemDescription { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string AssignedStaffName { get; init; } = string.Empty;

    public int ReviewCount { get; init; }
}

public sealed class SalesInvoiceHistoryItemDto
{
    public Guid InvoiceId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public DateTimeOffset InvoiceDate { get; init; }

    public string Status { get; init; } = string.Empty;

    public decimal SubTotal { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public string CreatedByStaffName { get; init; } = string.Empty;

    public List<SalesInvoiceItemDetailDto> Items { get; init; } = [];

    public decimal AmountPaid { get; init; }

    public decimal AmountDue { get; init; }
}

public sealed class SalesInvoiceItemDetailDto
{
    public string Description { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}
