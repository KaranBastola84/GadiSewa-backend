using GadiSewa.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class CreatePurchaseInvoiceRequestDto
{
    [Required]
    public Guid VendorId { get; init; }

    public DateTimeOffset InvoiceDate { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DueDate { get; init; }

    [Range(typeof(decimal), "0", "100")]
    public decimal TaxRatePercent { get; init; } = 13m;

    public InvoiceStatus Status { get; init; } = InvoiceStatus.Unpaid;

    [MinLength(1)]
    public List<PurchaseInvoiceItemInputDto> Items { get; init; } = [];
}
