using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public sealed class EmailVerificationToken : BaseEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public User User { get; set; } = null!;
}