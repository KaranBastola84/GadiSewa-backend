using GadiSewa.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.SalesInvoices;

public sealed class UpdateSalesInvoiceRequestDto
{
    [Required]
    public Guid CustomerId { get; init; }

    public Guid? AppointmentId { get; init; }

    public DateTimeOffset InvoiceDate { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DueDate { get; init; }

    [Range(typeof(decimal), "0", "100")]
    public decimal TaxRatePercent { get; init; } = 13m;

    public InvoiceStatus Status { get; init; } = InvoiceStatus.Unpaid;

    [MinLength(1)]
    public List<SalesInvoiceItemInputDto> Items { get; init; } = [];
}
