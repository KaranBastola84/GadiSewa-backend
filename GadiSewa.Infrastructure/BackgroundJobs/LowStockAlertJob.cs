using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.Infrastructure.BackgroundJobs;

public sealed class LowStockAlertJob
{
    private const int LowStockThreshold = 10;

    private readonly IRepository<Part> _partRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRepository<NotificationLog> _notificationLogRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public LowStockAlertJob(
        IRepository<Part> partRepository,
        IUserRepository userRepository,
        IRepository<NotificationLog> notificationLogRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _partRepository = partRepository;
        _userRepository = userRepository;
        _notificationLogRepository = notificationLogRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    public async Task RunAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var lowStockParts = await _partRepository.Query()
            .Where(p => p.StockQuantity < LowStockThreshold)
            .ToListAsync();

        if (lowStockParts.Count == 0)
        {
            return;
        }

        var admins = await _userRepository.ListAsync(u => u.Role == UserRole.Admin && u.IsActive);
        if (admins.Count == 0)
        {
            return;
        }

        var partIds = lowStockParts.Select(p => p.Id).ToList();
        var startOfTodayUtc = new DateTimeOffset(now.Date, TimeSpan.Zero);

        var alreadyNotified = await _notificationLogRepository.Query()
            .Where(log =>
                log.NotificationType == "LowStockAlert"
                && log.RelatedEntityType == "Part"
                && log.RelatedEntityId.HasValue
                && partIds.Contains(log.RelatedEntityId.Value)
                && log.SentAt >= startOfTodayUtc)
            .Select(log => new { log.RelatedEntityId, log.Recipient })
            .ToListAsync();

        var notifiedLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in alreadyNotified)
        {
            if (entry.RelatedEntityId.HasValue)
            {
                notifiedLookup.Add($"{entry.RelatedEntityId.Value:N}|{entry.Recipient}");
            }
        }

        var hasMutations = false;

        foreach (var part in lowStockParts)
        {
            foreach (var admin in admins)
            {
                var key = $"{part.Id:N}|{admin.Email}";
                if (notifiedLookup.Contains(key))
                {
                    continue;
                }

                await _emailService.SendLowStockAlertAsync(admin.Email, part.Name, part.StockQuantity);

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
                    SentAt = now
                });

                notifiedLookup.Add(key);
                hasMutations = true;
            }
        }

        if (hasMutations)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
