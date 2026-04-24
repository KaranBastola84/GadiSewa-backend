using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class Customer : BaseEntity
{
    public Guid UserId { get; set; }

    public string Address { get; set; } = string.Empty;

    public int LoyaltyPoints { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();

    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    public ICollection<CreditPayment> CreditPayments { get; set; } = new List<CreditPayment>();
}
