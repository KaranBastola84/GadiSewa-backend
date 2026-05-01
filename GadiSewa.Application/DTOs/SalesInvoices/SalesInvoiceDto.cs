using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.SalesInvoices;

public sealed class SalesInvoiceDto
{
    public Guid InvoiceId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public Guid CreatedByStaffId { get; init; }

    public string CreatedByStaffName { get; init; } = string.Empty;

    public Guid? AppointmentId { get; init; }

    public DateTimeOffset InvoiceDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    public decimal SubTotal { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool LoyaltyDiscountApplied { get; init; }

    public List<SalesInvoiceItemDto> Items { get; init; } = [];

    public static SalesInvoiceDto FromEntity(
        GadiSewa.Domain.Entities.SalesInvoice invoice,
        IEnumerable<SalesInvoiceItemDto> items,
        bool loyaltyDiscountApplied)
    {
        return new SalesInvoiceDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerId = invoice.CustomerId,
            CustomerName = invoice.Customer.User.FirstName + " " + invoice.Customer.User.LastName,
            CreatedByStaffId = invoice.CreatedByStaffId,
            CreatedByStaffName = $"{invoice.CreatedByStaff.User.FirstName} {invoice.CreatedByStaff.User.LastName}".Trim(),
            AppointmentId = invoice.AppointmentId,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            SubTotal = invoice.SubTotal,
            DiscountAmount = invoice.DiscountAmount,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status.ToString(),
            LoyaltyDiscountApplied = loyaltyDiscountApplied,
            Items = items.ToList()
        };
    }
}

public sealed class SalesInvoiceItemDto
{
    public Guid ItemId { get; init; }

    public Guid? PartId { get; init; }

    public string Description { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }

    public static SalesInvoiceItemDto FromEntity(GadiSewa.Domain.Entities.SalesInvoiceItem item)
    {
        return new SalesInvoiceItemDto
        {
            ItemId = item.Id,
            PartId = item.PartId,
            Description = item.Description,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            LineTotal = item.LineTotal
        };
    }
}
