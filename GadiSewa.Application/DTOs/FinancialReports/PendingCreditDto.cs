namespace GadiSewa.Application.DTOs.FinancialReports;

public sealed class PendingCreditDto
{
    public Guid InvoiceId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public DateTimeOffset InvoiceDate { get; init; }

    public DateTimeOffset DueDate { get; init; }

    public decimal TotalAmount { get; init; }

    public decimal AmountPaid { get; init; }

    public decimal AmountDue { get; init; }

    public int DaysOverdue { get; init; }
}
