using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.PurchaseInvoices;

public sealed class ReceiveStockRequestDto
{
    [Required(ErrorMessage = "Items to receive are required.")]
    [MinLength(1, ErrorMessage = "At least one item must be received.")]
    public List<ReceiveStockItemDto> Items { get; init; } = [];
}

public sealed class ReceiveStockItemDto
{
    [Required(ErrorMessage = "Purchase invoice item ID is required.")]
    public Guid ItemId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity received must be at least 1.")]
    public int QuantityReceived { get; init; }
}
