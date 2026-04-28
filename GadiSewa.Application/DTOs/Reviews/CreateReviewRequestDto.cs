using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Reviews;

public sealed class CreateReviewRequestDto
{
    [Required]
    public Guid AppointmentId { get; init; }

    [Range(1, 5)]
    public int Rating { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Comment { get; init; } = string.Empty;
}
