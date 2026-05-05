namespace GadiSewa.Application.DTOs.FinancialReports;

public sealed class TopSpenderDto
{
    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public decimal TotalSpent { get; init; }

    public int PurchaseCount { get; init; }

    public decimal AverageOrderValue { get; init; }

    public DateTimeOffset? LastPurchaseDate { get; init; }
}
