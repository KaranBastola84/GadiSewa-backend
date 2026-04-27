using GadiSewa.Domain.Common;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Domain.Entities;

public class Appointment : BaseEntity
{
    public string AppointmentNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid VehicleId { get; set; }

    public Guid? AssignedStaffId { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string ProblemDescription { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    public Customer Customer { get; set; } = null!;

    public Vehicle Vehicle { get; set; } = null!;

    public Staff? AssignedStaff { get; set; }

    public SalesInvoice? SalesInvoice { get; set; }

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
