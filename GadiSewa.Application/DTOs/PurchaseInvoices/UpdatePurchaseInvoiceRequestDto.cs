using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class UpdatePurchaseInvoiceRequestDto
{
    public Guid VendorId { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "Tax amount must be non-negative.")]
    public decimal TaxAmount { get; init; }
}
