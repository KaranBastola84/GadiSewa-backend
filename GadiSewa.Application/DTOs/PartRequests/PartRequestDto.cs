using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.PartRequests;

public sealed class PartRequestDto
{
    public Guid Id { get; init; }

    public string RequestNumber { get; init; } = string.Empty;

    public Guid? RequestedByStaffId { get; init; }

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

    public string VehicleModel { get; init; } = string.Empty;

    public string Brand { get; init; } = string.Empty;

    public string Urgency { get; init; } = string.Empty;

    public string RequestedByName { get; init; } = string.Empty;

    public DateTimeOffset RequestedDate { get; init; }

    public static PartRequestDto FromPartRequest(PartRequest partRequest)
    {
        var vehicleModel = string.Empty;
        var brand = string.Empty;
        var urgency = "Medium";
        var cleanNotes = partRequest.Notes;

        if (partRequest.Notes != null && partRequest.Notes.StartsWith("Vehicle: "))
        {
            var parts = partRequest.Notes.Split(" | ");
            foreach (var part in parts)
            {
                if (part.StartsWith("Vehicle: "))
                {
                    vehicleModel = part.Substring("Vehicle: ".Length);
                }
                else if (part.StartsWith("Brand: "))
                {
                    brand = part.Substring("Brand: ".Length);
                }
                else if (part.StartsWith("Urgency: "))
                {
                    urgency = part.Substring("Urgency: ".Length);
                }
                else if (part.StartsWith("Notes: "))
                {
                    cleanNotes = part.Substring("Notes: ".Length);
                }
            }
        }

        var requestedByName = string.Empty;
        if (partRequest.RequestedByStaff?.User is not null)
        {
            requestedByName = $"{partRequest.RequestedByStaff.User.FirstName} {partRequest.RequestedByStaff.User.LastName}".Trim();
        }
        else if (partRequest.RequestedByCustomer?.User is not null)
        {
            requestedByName = $"{partRequest.RequestedByCustomer.User.FirstName} {partRequest.RequestedByCustomer.User.LastName}".Trim();
        }

        var notesDisplay = cleanNotes;
        if (!string.IsNullOrWhiteSpace(vehicleModel))
        {
            var details = $"Vehicle: {vehicleModel}" + 
                          (string.IsNullOrWhiteSpace(brand) ? "" : $", Brand: {brand}") + 
                          (string.IsNullOrWhiteSpace(urgency) ? "" : $", Urgency: {urgency}");
            notesDisplay = string.IsNullOrWhiteSpace(cleanNotes) ? details : $"{cleanNotes} ({details})";
        }

        return new PartRequestDto
        {
            Id = partRequest.Id,
            RequestNumber = partRequest.RequestNumber,
            RequestedByStaffId = partRequest.RequestedByStaffId,
            RequestedByStaffName = partRequest.RequestedByStaff?.User is null
                ? string.Empty
                : $"{partRequest.RequestedByStaff.User.FirstName} {partRequest.RequestedByStaff.User.LastName}".Trim(),
            PartId = partRequest.PartId,
            PartName = partRequest.Part?.Name ?? string.Empty,
            PartNumber = partRequest.Part?.PartNumber ?? string.Empty,
            VendorId = partRequest.VendorId,
            VendorName = partRequest.Vendor?.Name,
            QuantityRequested = partRequest.QuantityRequested,
            NeededBy = partRequest.NeededBy,
            Status = partRequest.Status,
            Notes = notesDisplay,
            CreatedAt = partRequest.CreatedAt,
            UpdatedAt = partRequest.UpdatedAt,
            VehicleModel = vehicleModel,
            Brand = brand,
            Urgency = urgency,
            RequestedByName = requestedByName,
            RequestedDate = partRequest.CreatedAt
        };
    }
}
