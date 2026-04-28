using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.Appointments;

public class AppointmentDto
{
    public Guid Id { get; set; }

    public string AppointmentNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public Guid VehicleId { get; set; }

    public string VehicleName { get; set; } = string.Empty; // Make + Model + Year

    public string RegistrationNumber { get; set; } = string.Empty;

    public Guid? AssignedStaffId { get; set; }

    public string? AssignedStaffName { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string ProblemDescription { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static AppointmentDto FromAppointment(Appointment appointment)
    {
        return new AppointmentDto
        {
            Id = appointment.Id,
            AppointmentNumber = appointment.AppointmentNumber,
            CustomerId = appointment.CustomerId,
            CustomerName = $"{appointment.Customer.User.FirstName} {appointment.Customer.User.LastName}",
            CustomerPhone = appointment.Customer.User.PhoneNumber,
            VehicleId = appointment.VehicleId,
            VehicleName = $"{appointment.Vehicle.Make} {appointment.Vehicle.Model} ({appointment.Vehicle.Year})",
            RegistrationNumber = appointment.Vehicle.RegistrationNumber,
            AssignedStaffId = appointment.AssignedStaffId,
            AssignedStaffName = appointment.AssignedStaff != null
                ? $"{appointment.AssignedStaff.User.FirstName} {appointment.AssignedStaff.User.LastName}"
                : null,
            ScheduledAt = appointment.ScheduledAt,
            CompletedAt = appointment.CompletedAt,
            ProblemDescription = appointment.ProblemDescription,
            Notes = appointment.Notes,
            Status = appointment.Status,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt
        };
    }
}
