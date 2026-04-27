using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public class Review : BaseEntity
{
    public Guid CustomerId { get; set; }

    public Guid AppointmentId { get; set; }

    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;

    public Customer Customer { get; set; } = null!;

    public Appointment Appointment { get; set; } = null!;
}
