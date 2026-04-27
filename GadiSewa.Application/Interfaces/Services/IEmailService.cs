namespace GadiSewa.Application.Interfaces.Services;

public interface IEmailService
{
    Task SendEmailVerificationEmailAsync(string email, string fullName, string verificationToken, CancellationToken cancellationToken = default);

    Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(string email, string fullName, string resetToken, CancellationToken cancellationToken = default);
}
