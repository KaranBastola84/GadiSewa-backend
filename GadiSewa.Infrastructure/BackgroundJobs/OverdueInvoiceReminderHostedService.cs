using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GadiSewa.Infrastructure.BackgroundJobs;

public sealed class OverdueInvoiceReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueInvoiceReminderHostedService> _logger;

    public OverdueInvoiceReminderHostedService(IServiceScopeFactory scopeFactory, ILogger<OverdueInvoiceReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckOverdueInvoicesAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
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
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var cutoff = DateTimeOffset.UtcNow.AddMonths(-1);
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

                invoice.OverdueReminderSentAt = DateTimeOffset.UtcNow;
                invoiceRepository.Update(invoice);
            }

            if (overdueInvoices.Count > 0)
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking overdue sales invoices.");
        }
    }
}
