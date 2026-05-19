using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.Infrastructure.BackgroundJobs;

public sealed class OverdueCreditReminderJob
{
    private readonly IRepository<SalesInvoice> _invoiceRepository;
    private readonly IRepository<NotificationLog> _notificationLogRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public OverdueCreditReminderJob(
        IRepository<SalesInvoice> invoiceRepository,
        IRepository<NotificationLog> notificationLogRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _invoiceRepository = invoiceRepository;
        _notificationLogRepository = notificationLogRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    public async Task RunAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddMonths(-1);
        var overdueInvoices = await _invoiceRepository.Query()
            .Include(i => i.Customer)
                .ThenInclude(c => c.User)
            .Where(i => i.AmountDue > 0)
            .Where(i => i.DueDate.HasValue && i.DueDate.Value <= cutoff)
            .Where(i => i.OverdueReminderSentAt == null)
            .ToListAsync();

        if (overdueInvoices.Count == 0)
        {
            return;
        }

        foreach (var invoice in overdueInvoices)
        {
            if (invoice.Status != InvoiceStatus.Paid)
            {
                invoice.Status = InvoiceStatus.Overdue;
            }

            await _emailService.SendOverdueReminderEmailAsync(
                invoice.Customer.User.Email,
                $"{invoice.Customer.User.FirstName} {invoice.Customer.User.LastName}".Trim(),
                invoice.InvoiceNumber,
                invoice.AmountDue,
                invoice.DueDate!.Value);

            await _notificationLogRepository.AddAsync(new NotificationLog
            {
                NotificationType = "OverdueCreditReminder",
                Channel = "Email",
                Recipient = invoice.Customer.User.Email,
                Subject = $"Overdue payment reminder for invoice {invoice.InvoiceNumber}",
                Message = $"Outstanding amount {invoice.AmountDue:N2} for invoice {invoice.InvoiceNumber}.",
                IsSuccess = true,
                RelatedEntityType = "SalesInvoice",
                RelatedEntityId = invoice.Id,
                SentAt = now
            });

            invoice.OverdueReminderSentAt = now;
            _invoiceRepository.Update(invoice);
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
