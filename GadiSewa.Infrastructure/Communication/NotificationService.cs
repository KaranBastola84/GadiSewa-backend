using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.Infrastructure.Communication;

public sealed class NotificationService : INotificationService
{
    private const int LowStockThreshold = 10;

    private readonly IRepository<Part> _partRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRepository<NotificationLog> _notificationLogRepository;
    private readonly IEmailService _emailService;
    private readonly IRealtimeNotificationPublisher _realtimeNotificationPublisher;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(
        IRepository<Part> partRepository,
        IUserRepository userRepository,
        IRepository<NotificationLog> notificationLogRepository,
        IEmailService emailService,
        IRealtimeNotificationPublisher realtimeNotificationPublisher,
        IUnitOfWork unitOfWork)
    {
        _partRepository = partRepository;
        _userRepository = userRepository;
        _notificationLogRepository = notificationLogRepository;
        _emailService = emailService;
        _realtimeNotificationPublisher = realtimeNotificationPublisher;
        _unitOfWork = unitOfWork;
    }

    public async Task CheckAndNotifyLowStockAsync(IEnumerable<Guid> partIds, CancellationToken cancellationToken = default)
    {
        var normalizedPartIds = partIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalizedPartIds.Count == 0)
        {
            return;
        }

        var parts = await _partRepository.Query()
            .Where(p => normalizedPartIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (parts.Count == 0)
        {
            return;
        }

        var admins = await _userRepository.ListAsync(
            u => u.Role == UserRole.Admin && u.IsActive,
            cancellationToken);

        var hasMutations = false;

        foreach (var part in parts)
        {
            if (part.StockQuantity < LowStockThreshold)
            {
                if (part.LowStockNotified)
                {
                    continue;
                }

                foreach (var admin in admins)
                {
                    await _emailService.SendLowStockAlertAsync(admin.Email, part.Name, part.StockQuantity, cancellationToken);

                    await _notificationLogRepository.AddAsync(new NotificationLog
                    {
                        NotificationType = "LowStockAlert",
                        Channel = "Email",
                        Recipient = admin.Email,
                        Subject = $"Low stock alert: {part.Name}",
                        Message = $"Part '{part.Name}' is below threshold ({part.StockQuantity} < {LowStockThreshold}).",
                        IsSuccess = true,
                        RelatedEntityType = "Part",
                        RelatedEntityId = part.Id,
                        SentAt = DateTimeOffset.UtcNow
                    }, cancellationToken);
                }

                await _realtimeNotificationPublisher.NotifyAdminsLowStockAsync(
                    part.Id,
                    part.Name,
                    part.StockQuantity,
                    LowStockThreshold,
                    cancellationToken);

                part.LowStockNotified = true;
                part.UpdatedAt = DateTimeOffset.UtcNow;
                _partRepository.Update(part);
                hasMutations = true;
            }
            else if (part.LowStockNotified)
            {
                part.LowStockNotified = false;
                part.UpdatedAt = DateTimeOffset.UtcNow;
                _partRepository.Update(part);
                hasMutations = true;
            }
        }

        if (hasMutations)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public Task NotifySaleCreatedAsync(
        Guid salesInvoiceId,
        string invoiceNumber,
        Guid customerId,
        decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        return _realtimeNotificationPublisher.NotifyStaffSaleCreatedAsync(
            salesInvoiceId,
            invoiceNumber,
            customerId,
            totalAmount,
            cancellationToken);
    }
}
