using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class CreateStaffRequestDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(20)]
    public string PhoneNumber { get; init; } = string.Empty;

    [MaxLength(50)]
    [SwaggerSchema(Description = "Optional. If not provided, an employee code will be auto-generated in the format: EMPyyyyMMddNNN")]
    public string EmployeeCode { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Position { get; init; } = string.Empty;

    [Required]
    public DateOnly HireDate { get; init; }
}