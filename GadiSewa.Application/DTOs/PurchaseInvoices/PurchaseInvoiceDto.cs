namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class PurchaseInvoiceDto
{
    public Guid PurchaseInvoiceId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid VendorId { get; init; }

    public string VendorName { get; init; } = string.Empty;

    public Guid CreatedByStaffId { get; init; }

    public string CreatedByStaffName { get; init; } = string.Empty;

    public DateTimeOffset InvoiceDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    public decimal SubTotal { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public string Status { get; init; } = string.Empty;

    public List<PurchaseInvoiceItemDto> Items { get; init; } = [];
}
