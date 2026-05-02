namespace GadiSewa.Application.DTOs.SalesInvoices;

public sealed class SalesInvoiceDto
{
    public Guid SalesInvoiceId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerEmail { get; init; } = string.Empty;

    public Guid CreatedByStaffId { get; init; }

    public string CreatedByStaffName { get; init; } = string.Empty;

    public Guid? AppointmentId { get; init; }

    public DateTimeOffset InvoiceDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    public decimal SubTotal { get; init; }

    public decimal DiscountAmount { get; init; }

    public bool LoyaltyApplied { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public decimal AmountDue { get; init; }

    public string Status { get; init; } = string.Empty;

    public List<SalesInvoiceItemDto> Items { get; init; } = [];
}
