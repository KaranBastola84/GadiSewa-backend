namespace GadiSewa.Application.DTOs.SalesInvoices;

public sealed class SalesInvoiceItemDto
{
    public Guid SalesInvoiceItemId { get; init; }

    public Guid? PartId { get; init; }

    public string PartName { get; init; } = string.Empty;

    public string PartNumber { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}
