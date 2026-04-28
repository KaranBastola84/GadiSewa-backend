using System.ComponentModel.DataAnnotations;

namespace GadiSewa.Application.DTOs.Auth;

public sealed class UpdateUserStatusRequestDto
{
    [Required]
    public bool IsActive { get; init; }
}