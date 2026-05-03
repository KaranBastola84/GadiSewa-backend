using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.CreditPayments;

public sealed class CreateCreditPaymentRequestDto
{
    [Required]
    public Guid SalesInvoiceId { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; init; }

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; init; } = string.Empty;

    [MaxLength(100)]
    public string ReferenceNumber { get; init; } = string.Empty;

    public bool IsVerified { get; init; } = true;

    [MaxLength(500)]
    public string Notes { get; init; } = string.Empty;
}
