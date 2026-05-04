namespace GadiSewa.Application.DTOs.Notifications;

public sealed class NotificationLogPagedResultDto
{
    public IReadOnlyList<NotificationLogDto> Items { get; init; } = [];

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }
}
