using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class CreatePurchaseInvoiceRequestDto
{
    [Required(ErrorMessage = "Vendor ID is required.")]
    public Guid VendorId { get; init; }

    [Required(ErrorMessage = "Invoice date is required.")]
    public DateTimeOffset InvoiceDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "Tax amount must be non-negative.")]
    public decimal TaxAmount { get; init; }

    [Required(ErrorMessage = "At least one item is required.")]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<CreatePurchaseInvoiceItemRequestDto> Items { get; init; } = [];
}

public sealed class CreatePurchaseInvoiceItemRequestDto
{
    [Required(ErrorMessage = "Part ID is required.")]
    public Guid PartId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; init; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Unit cost must be greater than 0.")]
    public decimal UnitCost { get; init; }
}
