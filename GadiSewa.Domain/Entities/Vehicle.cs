using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class Vehicle : BaseEntity
{
    public Guid CustomerId { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public string Make { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Mileage { get; set; }

    public string Color { get; set; } = string.Empty;

    public Customer Customer { get; set; } = null!;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
