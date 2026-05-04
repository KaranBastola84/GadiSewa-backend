using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Notifications;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminNotificationsController : ControllerBase
{
    private readonly IRepository<NotificationLog> _notificationLogRepository;

    public AdminNotificationsController(IRepository<NotificationLog> notificationLogRepository)
    {
        _notificationLogRepository = notificationLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationLogDto>>>> GetNotifications(
        [FromQuery] string? type,
        [FromQuery] string? channel,
        [FromQuery] string? recipient,
        [FromQuery] bool? isSuccess,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var query = _notificationLogRepository.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim().ToUpperInvariant();
            query = query.Where(n => n.NotificationType.ToUpper().Contains(normalizedType));
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            var normalizedChannel = channel.Trim().ToUpperInvariant();
            query = query.Where(n => n.Channel.ToUpper().Contains(normalizedChannel));
        }

        if (!string.IsNullOrWhiteSpace(recipient))
        {
            var normalizedRecipient = recipient.Trim().ToUpperInvariant();
            query = query.Where(n => n.Recipient.ToUpper().Contains(normalizedRecipient));
        }

        if (isSuccess.HasValue)
        {
            query = query.Where(n => n.IsSuccess == isSuccess.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(n => n.SentAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(n => n.SentAt <= to.Value);
        }

        var notifications = await query
            .OrderByDescending(n => n.SentAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = notifications
            .Select(NotificationLogDto.FromEntity)
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<NotificationLogDto>>.Success(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<NotificationLogDto>>> GetNotificationById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var notification = await _notificationLogRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notification is null)
        {
            return NotFound(ApiResponse<NotificationLogDto>.Failure("Notification log not found.", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<NotificationLogDto>.Success(NotificationLogDto.FromEntity(notification)));
    }
}
