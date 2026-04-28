using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.DTOs.Parts;

public sealed class PartDto
{
    public Guid PartId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string PartNumber { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public int StockQuantity { get; init; }

    public int ReorderLevel { get; init; }

    public bool IsLowStock => StockQuantity < ReorderLevel;

    public static PartDto FromPart(Part part)
    {
        return new PartDto
        {
            PartId = part.Id,
            Name = part.Name,
            PartNumber = part.PartNumber,
            Description = part.Description,
            UnitPrice = part.UnitPrice,
            StockQuantity = part.StockQuantity,
            ReorderLevel = part.ReorderLevel
        };
    }
}