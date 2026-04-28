using System.ComponentModel.DataAnnotations;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.Appointments;

public class UpdateAppointmentStatusRequestDto
{
    [Required(ErrorMessage = "Status is required")]
    public AppointmentStatus Status { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }

    public Guid? AssignedStaffId { get; set; }
}
