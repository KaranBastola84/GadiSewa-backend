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

    private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
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
            IsBodyHtml = false
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
