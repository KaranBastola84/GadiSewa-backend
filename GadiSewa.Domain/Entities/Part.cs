using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class Part : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string PartNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int StockQuantity { get; set; }

    public int ReorderLevel { get; set; }

    public ICollection<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; } = new List<PurchaseInvoiceItem>();

    public ICollection<SalesInvoiceItem> SalesInvoiceItems { get; set; } = new List<SalesInvoiceItem>();

    public ICollection<PartRequest> PartRequests { get; set; } = new List<PartRequest>();
}
