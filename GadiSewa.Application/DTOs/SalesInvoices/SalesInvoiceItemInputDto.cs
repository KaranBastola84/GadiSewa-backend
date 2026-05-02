using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.SalesInvoices;

public sealed class SalesInvoiceItemInputDto
{
    public Guid? PartId { get; init; }

    [MaxLength(300)]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal UnitPrice { get; init; }
}
