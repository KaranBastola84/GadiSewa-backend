using GadiSewa.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace GadiSewa.Infrastructure.Communication;

public sealed class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Welcome email queued for {Email} ({FullName})", email, fullName);
        return Task.CompletedTask;
    }
}
