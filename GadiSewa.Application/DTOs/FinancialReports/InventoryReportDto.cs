namespace GadiSewa.Application.DTOs.FinancialReports;

public sealed class InventoryReportDto
{
    public Guid PartId { get; init; }

    public string PartName { get; init; } = string.Empty;

    public string PartNumber { get; init; } = string.Empty;

    public int CurrentStock { get; init; }

    public decimal UnitCost { get; init; }

    public decimal StockValue { get; init; }

    public string Category { get; init; } = string.Empty;
}
