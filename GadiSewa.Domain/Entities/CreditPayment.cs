using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class CreditPayment : BaseEntity
{
    public Guid SalesInvoiceId { get; set; }

    public Guid CustomerId { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset PaymentDate { get; set; } = DateTimeOffset.UtcNow;

    public string PaymentMethod { get; set; } = string.Empty;

    public string ReferenceNumber { get; set; } = string.Empty;

    public bool IsVerified { get; set; }

    public string Notes { get; set; } = string.Empty;

    public SalesInvoice SalesInvoice { get; set; } = null!;

    public Customer Customer { get; set; } = null!;
}
