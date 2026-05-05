namespace GadiSewa.Application.DTOs.FinancialReports;

public sealed class FinancialSummaryDto
{
    public DateTimeOffset? StartDate { get; init; }

    public DateTimeOffset? EndDate { get; init; }

    public decimal TotalRevenue { get; init; }

    public decimal TotalTax { get; init; }

    public decimal TotalDiscounts { get; init; }

    public decimal NetRevenue { get; init; }

    public int InvoiceCount { get; init; }
}
