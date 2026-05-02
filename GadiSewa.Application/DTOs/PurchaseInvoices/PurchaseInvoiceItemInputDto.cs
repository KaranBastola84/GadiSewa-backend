using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class PurchaseInvoiceItemInputDto
{
    [Required]
    public Guid PartId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal UnitCost { get; init; }
}
