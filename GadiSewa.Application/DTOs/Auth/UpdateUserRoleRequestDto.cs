using System.ComponentModel.DataAnnotations;
using GadiSewa.Domain.Enums;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class UpdateUserRoleRequestDto
{
    [Required]
    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; }
}