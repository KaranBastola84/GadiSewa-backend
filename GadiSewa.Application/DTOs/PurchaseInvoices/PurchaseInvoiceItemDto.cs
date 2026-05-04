namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class PurchaseInvoiceItemDto
{
    public Guid PurchaseInvoiceItemId { get; init; }

    public Guid PartId { get; init; }

    public string PartName { get; init; } = string.Empty;

    public string PartNumber { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitCost { get; init; }

    public decimal LineTotal { get; init; }
}
