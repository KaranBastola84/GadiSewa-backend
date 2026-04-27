using GadiSewa.Domain.Common;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Domain.Entities;

public class PartRequest : BaseEntity
{
    public string RequestNumber { get; set; } = string.Empty;

    public Guid RequestedByStaffId { get; set; }

    public Guid PartId { get; set; }

    public Guid? VendorId { get; set; }

    public int QuantityRequested { get; set; }

    public DateTimeOffset NeededBy { get; set; }

    public PartRequestStatus Status { get; set; } = PartRequestStatus.Requested;

    public string Notes { get; set; } = string.Empty;

    public Staff RequestedByStaff { get; set; } = null!;

    public Part Part { get; set; } = null!;

    public Vendor? Vendor { get; set; }
}
