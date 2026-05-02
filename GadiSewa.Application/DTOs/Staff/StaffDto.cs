using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.DTOs.Staff;

public sealed class StaffDto
{
    public Guid StaffId { get; init; }

    public Guid UserId { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string EmployeeCode { get; init; } = string.Empty;

    public string Position { get; init; } = string.Empty;

    public DateOnly HireDate { get; init; }

    public bool IsAvailable { get; init; }

    public bool IsActive { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public static StaffDto FromStaff(Domain.Entities.Staff staff)
    {
        return new StaffDto
        {
            StaffId = staff.Id,
            UserId = staff.UserId,
            FirstName = staff.User.FirstName,
            LastName = staff.User.LastName,
            Email = staff.User.Email,
            PhoneNumber = staff.User.PhoneNumber,
            EmployeeCode = staff.EmployeeCode,
            Position = staff.Position,
            HireDate = staff.HireDate,
            IsAvailable = staff.IsAvailable,
            IsActive = staff.User.IsActive,
            LastLoginAt = staff.User.LastLoginAt,
            CreatedAt = staff.CreatedAt
        };
    }
}
