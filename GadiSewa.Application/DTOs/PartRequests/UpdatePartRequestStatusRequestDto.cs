using System.ComponentModel.DataAnnotations;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.PartRequests;

public sealed class UpdatePartRequestStatusRequestDto
{
    [Required]
    public PartRequestStatus Status { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public Guid? VendorId { get; init; }
}
