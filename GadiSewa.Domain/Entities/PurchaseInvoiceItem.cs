using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class PurchaseInvoiceItem : BaseEntity
{
    public Guid PurchaseInvoiceId { get; set; }

    public Guid PartId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }

    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;

    public Part Part { get; set; } = null!;
}
