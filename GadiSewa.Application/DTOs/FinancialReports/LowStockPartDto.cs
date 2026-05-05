namespace GadiSewa.Application.DTOs.FinancialReports;

public sealed class LowStockPartDto
{
    public Guid PartId { get; init; }

    public string PartName { get; init; } = string.Empty;

    public string PartNumber { get; init; } = string.Empty;

    public int CurrentStock { get; init; }

    public int MinimumStockLevel { get; init; }

    public int StockDeficit { get; init; } // MinimumStockLevel - CurrentStock

    public decimal UnitCost { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Status { get; init; } = "Low Stock"; // Status indicator
}
