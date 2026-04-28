using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.DTOs.Vendors;

public sealed class VendorDto
{
    public Guid VendorId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ContactPerson { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public static VendorDto FromVendor(Vendor vendor)
    {
        return new VendorDto
        {
            VendorId = vendor.Id,
            Name = vendor.Name,
            ContactPerson = vendor.ContactPerson,
            Email = vendor.Email,
            PhoneNumber = vendor.PhoneNumber,
            Address = vendor.Address
        };
    }
}
