using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GadiSewa.Infrastructure.BackgroundJobs;

public sealed class OverdueInvoiceReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueInvoiceReminderHostedService> _logger;
    private readonly NotificationOptions _notificationOptions;

    public OverdueInvoiceReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OverdueInvoiceReminderHostedService> logger,
        IOptions<NotificationOptions> notificationOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _notificationOptions = notificationOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckOverdueInvoicesAsync(stoppingToken);

        var intervalDays = Math.Max(1, _notificationOptions.OverdueReminderIntervalInDays);
        using var timer = new PeriodicTimer(TimeSpan.FromDays(intervalDays));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckOverdueInvoicesAsync(stoppingToken);
        }
    }

    private async Task CheckOverdueInvoicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var invoiceRepository = scope.ServiceProvider.GetRequiredService<IRepository<SalesInvoice>>();
            var notificationLogRepository = scope.ServiceProvider.GetRequiredService<IRepository<NotificationLog>>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cutoff = DateTimeOffset.UtcNow;
            var overdueInvoices = await invoiceRepository.Query()
                .Include(i => i.Customer)
                    .ThenInclude(c => c.User)
                .Where(i => i.AmountDue > 0 && i.DueDate.HasValue && i.DueDate.Value < cutoff)
                .Where(i => i.OverdueReminderSentAt == null)
                .ToListAsync(cancellationToken);

            foreach (var invoice in overdueInvoices)
            {
                if (invoice.Status != InvoiceStatus.Paid)
                {
                    invoice.Status = InvoiceStatus.Overdue;
                }

                await emailService.SendOverdueReminderEmailAsync(
                    invoice.Customer.User.Email,
                    $"{invoice.Customer.User.FirstName} {invoice.Customer.User.LastName}".Trim(),
                    invoice.InvoiceNumber,
                    invoice.AmountDue,
                    invoice.DueDate!.Value,
                    cancellationToken);

                await notificationLogRepository.AddAsync(new NotificationLog
                {
                    NotificationType = "OverdueCreditReminder",
                    Channel = "Email",
                    Recipient = invoice.Customer.User.Email,
                    Subject = $"Overdue payment reminder for invoice {invoice.InvoiceNumber}",
                    Message = $"Outstanding amount {invoice.AmountDue:N2} for invoice {invoice.InvoiceNumber}.",
                    IsSuccess = true,
                    RelatedEntityType = "SalesInvoice",
                    RelatedEntityId = invoice.Id,
                    SentAt = DateTimeOffset.UtcNow
                }, cancellationToken);

                invoice.OverdueReminderSentAt = DateTimeOffset.UtcNow;
                invoiceRepository.Update(invoice);
            }

            if (overdueInvoices.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking overdue sales invoices.");
        }
    }
}
