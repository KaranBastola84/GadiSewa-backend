namespace GadiSewa.Application.DTOs.CreditPayments;

public sealed class CreditPaymentDto
{
    public Guid CreditPaymentId { get; init; }

    public Guid SalesInvoiceId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public decimal AmountBeforePayment { get; init; }

    public decimal AmountAfterPayment { get; init; }

    public DateTimeOffset PaymentDate { get; init; }

    public string PaymentMethod { get; init; } = string.Empty;

    public string ReferenceNumber { get; init; } = string.Empty;

    public bool IsVerified { get; init; }

    public string Notes { get; init; } = string.Empty;
}
