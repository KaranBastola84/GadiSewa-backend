using System.Security.Claims;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : Guid.Empty;
    }

    public static async Task<Guid> GetStaffIdAsync(this ClaimsPrincipal user, IRepository<Staff> staffRepository, CancellationToken cancellationToken = default)
    {
        var userId = user.GetUserId();
        if (userId == Guid.Empty) return Guid.Empty;

        return await staffRepository.Query()
            .Where(s => s.UserId == userId)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
