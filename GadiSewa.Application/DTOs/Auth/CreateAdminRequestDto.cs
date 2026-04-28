using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class CreateAdminRequestDto
{
    [Required]
    [MaxLength(100)]
    [DefaultValue("Aarav")]
    [SwaggerSchema(Description = "Admin first name. Example: Aarav")]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [DefaultValue("Shrestha")]
    [SwaggerSchema(Description = "Admin last name. Example: Shrestha")]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [DefaultValue("admin@gadisewa.com")]
    [SwaggerSchema(Description = "Admin email address. Example: admin@gadisewa.com")]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [DefaultValue("Admin@12345")]
    [SwaggerSchema(Description = "Admin password. Example: Admin@12345")]
    public string Password { get; init; } = string.Empty;

    [MaxLength(20)]
    [DefaultValue("9800000000")]
    [SwaggerSchema(Description = "Contact phone number. Example: 9800000000")]
    public string PhoneNumber { get; init; } = string.Empty;
}