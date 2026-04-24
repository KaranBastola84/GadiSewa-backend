using GadiSewa.Domain.Interfaces;

namespace GadiSewa.Domain.Common;

public abstract class BaseEntity : IEntity, IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
