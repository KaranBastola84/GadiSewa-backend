using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Parts;

public sealed class UpdatePartRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string PartNumber { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; init; }
}