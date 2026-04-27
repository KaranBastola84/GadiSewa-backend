using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class SalesInvoiceItem : BaseEntity
{
    public Guid SalesInvoiceId { get; set; }

    public Guid? PartId { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public SalesInvoice SalesInvoice { get; set; } = null!;

    public Part? Part { get; set; }
}
