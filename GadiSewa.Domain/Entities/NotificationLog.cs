using GadiSewa.Domain.Common;

namespace GadiSewa.Domain.Entities;

public sealed class NotificationLog : BaseEntity
{
    public string NotificationType { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? Error { get; set; }

    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public DateTimeOffset SentAt { get; set; }
}
