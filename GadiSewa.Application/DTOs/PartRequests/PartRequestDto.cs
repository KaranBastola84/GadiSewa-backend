using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.PartRequests;

public sealed class PartRequestDto
{
    public Guid Id { get; init; }

    public string RequestNumber { get; init; } = string.Empty;

    public Guid RequestedByStaffId { get; init; }

    public string RequestedByStaffName { get; init; } = string.Empty;

    public Guid PartId { get; init; }

    public string PartName { get; init; } = string.Empty;

    public string PartNumber { get; init; } = string.Empty;

    public Guid? VendorId { get; init; }

    public string? VendorName { get; init; }

    public int QuantityRequested { get; init; }

    public DateTimeOffset NeededBy { get; init; }

    public PartRequestStatus Status { get; init; }

    public string Notes { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public static PartRequestDto FromPartRequest(PartRequest partRequest)
    {
        return new PartRequestDto
        {
            Id = partRequest.Id,
            RequestNumber = partRequest.RequestNumber,
            RequestedByStaffId = partRequest.RequestedByStaffId,
            RequestedByStaffName = $"{partRequest.RequestedByStaff.User.FirstName} {partRequest.RequestedByStaff.User.LastName}".Trim(),
            PartId = partRequest.PartId,
            PartName = partRequest.Part.Name,
            PartNumber = partRequest.Part.PartNumber,
            VendorId = partRequest.VendorId,
            VendorName = partRequest.Vendor?.Name,
            QuantityRequested = partRequest.QuantityRequested,
            NeededBy = partRequest.NeededBy,
            Status = partRequest.Status,
            Notes = partRequest.Notes,
            CreatedAt = partRequest.CreatedAt,
            UpdatedAt = partRequest.UpdatedAt
        };
    }
}
