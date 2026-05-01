namespace GadiSewa.Application.DTOs.FinancialReports;

public sealed class FinancialReportDto
{
    public string ReportType { get; init; } = string.Empty; // Daily, Monthly, Yearly

    public DateTimeOffset? StartDate { get; init; }

    public DateTimeOffset? EndDate { get; init; }

    public List<FinancialReportLineDto> Lines { get; init; } = [];

    public decimal TotalRevenue { get; init; }

    public decimal TotalCosts { get; init; }

    public decimal TotalProfit { get; init; }

    public decimal ProfitMargin { get; init; } // %
}

public sealed class FinancialReportLineDto
{
    public string Period { get; init; } = string.Empty; // Date for daily, Month for monthly, Year for yearly

    public decimal Revenue { get; init; }

    public decimal Costs { get; init; }

    public decimal Profit { get; init; }

    public decimal ProfitMargin { get; init; } // %

    public int TransactionCount { get; init; }
}
