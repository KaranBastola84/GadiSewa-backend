using System.ComponentModel.DataAnnotations;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class UpdatePurchaseInvoiceRequestDto
{
    public Guid VendorId { get; init; }

    public DateTimeOffset InvoiceDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "Tax amount must be non-negative.")]
    public decimal TaxAmount { get; init; }

    [Range(0, 100, ErrorMessage = "Tax rate must be between 0 and 100.")]
    public decimal TaxRatePercent { get; init; }

    public InvoiceStatus Status { get; init; } = InvoiceStatus.Unpaid;

    [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<CreatePurchaseInvoiceItemRequestDto> Items { get; init; } = [];
}
