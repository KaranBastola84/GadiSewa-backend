using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.PartRequests;

public sealed class CreatePartRequestRequestDto
{
    [Required]
    public Guid PartId { get; init; }

    public Guid? VendorId { get; init; }

    [Range(1, int.MaxValue)]
    public int QuantityRequested { get; init; }

    [Required]
    public DateTimeOffset NeededBy { get; init; }

    [MaxLength(1000)]
    public string Notes { get; init; } = string.Empty;
}
