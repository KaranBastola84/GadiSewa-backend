using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class Vendor : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string ContactPerson { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();

    public ICollection<PartRequest> PartRequests { get; set; } = new List<PartRequest>();
}
