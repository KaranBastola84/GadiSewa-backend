using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.SalesInvoices;

public sealed class CreateSalesInvoiceRequestDto
{
    [Required(ErrorMessage = "Customer ID is required.")]
    public Guid CustomerId { get; init; }

    public Guid? AppointmentId { get; init; }

    [Required(ErrorMessage = "Invoice date is required.")]
    public DateTimeOffset InvoiceDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "Tax amount must be non-negative.")]
    public decimal TaxAmount { get; init; }

    [Required(ErrorMessage = "At least one item is required.")]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<CreateSalesInvoiceItemRequestDto> Items { get; init; } = [];
}

public sealed class CreateSalesInvoiceItemRequestDto
{
    public Guid? PartId { get; init; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; init; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0.")]
    public decimal UnitPrice { get; init; }
}
