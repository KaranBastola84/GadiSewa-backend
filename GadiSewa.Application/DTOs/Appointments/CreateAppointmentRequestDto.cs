using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Appointments;

public class CreateAppointmentRequestDto
{
    [Required(ErrorMessage = "Vehicle ID is required")]
    public Guid VehicleId { get; set; }

    [Required(ErrorMessage = "Scheduled date and time is required")]
    public DateTimeOffset ScheduledAt { get; set; }

    [Required(ErrorMessage = "Problem description is required")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Problem description must be between 10 and 500 characters")]
    public string ProblemDescription { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }
}
