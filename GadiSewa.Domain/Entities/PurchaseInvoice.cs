using GadiSewa.Domain.Common;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Domain.Entities;

public class PurchaseInvoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid VendorId { get; set; }

    public Guid CreatedByStaffId { get; set; }

    public DateTimeOffset InvoiceDate { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DueDate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

    public Vendor Vendor { get; set; } = null!;

    public Staff CreatedByStaff { get; set; } = null!;

    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
}
