using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.DTOs.Notifications;

public sealed class NotificationLogDto
{
    public Guid Id { get; init; }

    public string NotificationType { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string Recipient { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool IsSuccess { get; init; }

    public string? Error { get; init; }

    public string? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    public DateTimeOffset SentAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public static NotificationLogDto FromEntity(NotificationLog entity)
    {
        return new NotificationLogDto
        {
            Id = entity.Id,
            NotificationType = entity.NotificationType,
            Channel = entity.Channel,
            Recipient = entity.Recipient,
            Subject = entity.Subject,
            Message = entity.Message,
            IsSuccess = entity.IsSuccess,
            Error = entity.Error,
            RelatedEntityType = entity.RelatedEntityType,
            RelatedEntityId = entity.RelatedEntityId,
            SentAt = entity.SentAt,
            CreatedAt = entity.CreatedAt
        };
    }
}
