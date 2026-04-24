using GadiSewa.Domain.Common;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Domain.Entities;

public class SalesInvoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid CreatedByStaffId { get; set; }

    public Guid? AppointmentId { get; set; }

    public DateTimeOffset InvoiceDate { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DueDate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

    public Customer Customer { get; set; } = null!;

    public Staff CreatedByStaff { get; set; } = null!;

    public Appointment? Appointment { get; set; }

    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();

    public ICollection<CreditPayment> CreditPayments { get; set; } = new List<CreditPayment>();
}
