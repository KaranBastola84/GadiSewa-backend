using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Customers;

public sealed class CustomerSearchRequestDto
{
    [StringLength(100)]
    public string? Query { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
