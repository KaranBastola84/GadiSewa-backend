namespace GadiSewa.Application.DTOs.CreditPayments;

public sealed class CustomerCreditHistoryDto
{
    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public decimal TotalPaid { get; init; }

    public decimal TotalOutstanding { get; init; }

    public List<CreditPaymentDto> Payments { get; init; } = [];
}
