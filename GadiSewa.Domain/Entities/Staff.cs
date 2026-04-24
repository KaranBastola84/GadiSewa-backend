using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class Staff : BaseEntity
{
    public Guid UserId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    public DateOnly HireDate { get; set; }

    public bool IsAvailable { get; set; } = true;

    public User User { get; set; } = null!;

    public ICollection<Appointment> AssignedAppointments { get; set; } = new List<Appointment>();

    public ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();

    public ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();

    public ICollection<PartRequest> PartRequests { get; set; } = new List<PartRequest>();
}
