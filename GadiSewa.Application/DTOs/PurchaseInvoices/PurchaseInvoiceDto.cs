using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class PurchaseInvoiceDto
{
    public Guid InvoiceId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid VendorId { get; init; }

    public string VendorName { get; init; } = string.Empty;

    public Guid CreatedByStaffId { get; init; }

    public string CreatedByStaffName { get; init; } = string.Empty;

    public DateTimeOffset InvoiceDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    public decimal SubTotal { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public string Status { get; init; } = string.Empty;

    public List<PurchaseInvoiceItemDto> Items { get; init; } = [];

    public static PurchaseInvoiceDto FromEntity(
        GadiSewa.Domain.Entities.PurchaseInvoice invoice,
        IEnumerable<PurchaseInvoiceItemDto> items)
    {
        return new PurchaseInvoiceDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            VendorId = invoice.VendorId,
            VendorName = invoice.Vendor.Name,
            CreatedByStaffId = invoice.CreatedByStaffId,
            CreatedByStaffName = $"{invoice.CreatedByStaff.User.FirstName} {invoice.CreatedByStaff.User.LastName}".Trim(),
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            SubTotal = invoice.SubTotal,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status.ToString(),
            Items = items.ToList()
        };
    }
}

public sealed class PurchaseInvoiceItemDto
{
    public Guid ItemId { get; init; }

    public Guid PartId { get; init; }

    public string PartNumber { get; init; } = string.Empty;

    public string PartName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitCost { get; init; }

    public decimal LineTotal { get; init; }

    public static PurchaseInvoiceItemDto FromEntity(GadiSewa.Domain.Entities.PurchaseInvoiceItem item)
    {
        return new PurchaseInvoiceItemDto
        {
            ItemId = item.Id,
            PartId = item.PartId,
            PartNumber = item.Part.PartNumber,
            PartName = item.Part.Name,
            Quantity = item.Quantity,
            UnitCost = item.UnitCost,
            LineTotal = item.LineTotal
        };
    }
}
