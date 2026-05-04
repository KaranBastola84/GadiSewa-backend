using GadiSewa.Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace GadiSewa.Infrastructure.Communication;

public sealed class EmailService : IEmailService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpOptions> smtpOptions, ILogger<EmailService> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    public Task SendEmailVerificationEmailAsync(string email, string fullName, string verificationToken, CancellationToken cancellationToken = default)
    {
        var subject = "Verify your GadiSewa email";
        var body = $"Hello {fullName},\n\nUse this token to verify your email address: {verificationToken}\n\nThis token will expire in 24 hours.\n\nRegards,\nGadiSewa Team";
        return SendEmailAsync(email, subject, body, cancellationToken);
    }

    public Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to GadiSewa";
        var body = $"Hello {fullName},\n\nYour account has been created successfully.\n\nRegards,\nGadiSewa Team";
        return SendEmailAsync(email, subject, body, cancellationToken);
    }

    public Task SendPasswordResetEmailAsync(string email, string fullName, string resetToken, CancellationToken cancellationToken = default)
    {
        var subject = "GadiSewa Password Reset";
        var body = $"Hello {fullName},\n\nUse this token to reset your password: {resetToken}\n\nThis token will expire in 1 hour.\n\nRegards,\nGadiSewa Team";
        return SendEmailAsync(email, subject, body, cancellationToken);
    }

    public Task SendLowStockAlertAsync(string toEmail, string partName, int stockQuantity, CancellationToken cancellationToken = default)
    {
        var subject = $"Low stock alert: {partName}";
        var body = $"Attention,\n\nThe part '{partName}' has low stock (current quantity: {stockQuantity}). Please reorder as soon as possible.\n\nRegards,\nGadiSewa System";
        return SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendSalesInvoiceEmailAsync(string toEmail, string customerName, string invoiceNumber, string invoiceHtml, CancellationToken cancellationToken = default)
    {
        var subject = $"Your GadiSewa Invoice {invoiceNumber}";
        return SendEmailAsync(toEmail, subject, invoiceHtml, cancellationToken, isBodyHtml: true);
    }

    public Task SendOverdueReminderEmailAsync(string toEmail, string customerName, string invoiceNumber, decimal amountDue, DateTimeOffset dueDate, CancellationToken cancellationToken = default)
    {
        var subject = $"Overdue payment reminder for invoice {invoiceNumber}";
        var body = $"<html><body style='font-family:Segoe UI,Arial,sans-serif;'>" +
                   $"<p>Hello {System.Net.WebUtility.HtmlEncode(customerName)},</p>" +
                   $"<p>This is a reminder that invoice <strong>{System.Net.WebUtility.HtmlEncode(invoiceNumber)}</strong> is overdue.</p>" +
                   $"<p><strong>Due date:</strong> {dueDate:yyyy-MM-dd}</p>" +
                   $"<p><strong>Outstanding amount:</strong> {amountDue:N2}</p>" +
                   $"<p>Please make the payment as soon as possible.</p>" +
                   $"<p>Regards,<br/>GadiSewa Team</p>" +
                   $"</body></html>";
        return SendEmailAsync(toEmail, subject, body, cancellationToken, isBodyHtml: true);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken, bool isBodyHtml = false)
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host) || string.IsNullOrWhiteSpace(_smtpOptions.FromEmail))
        {
            _logger.LogWarning("SMTP is not configured. Skipping email to {Email} with subject {Subject}.", toEmail, subject);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, string.IsNullOrWhiteSpace(_smtpOptions.FromName) ? _smtpOptions.FromEmail : _smtpOptions.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isBodyHtml
        };

        message.To.Add(toEmail);

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password)
        };

        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent to {Email} with subject {Subject}", toEmail, subject);
    }
}
