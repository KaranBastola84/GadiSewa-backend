namespace GadiSewa.Application.DTOs.FinancialReports;

public sealed class RegularCustomerDto
{
    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public int PurchaseCount { get; init; }

    public decimal TotalSpent { get; init; }

    public DateTimeOffset FirstPurchaseDate { get; init; }

    public DateTimeOffset? LastPurchaseDate { get; init; }

    public int LoyaltyPoints { get; init; }
}
