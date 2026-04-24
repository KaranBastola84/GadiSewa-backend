namespace GadiSewa.Application.Interfaces.Services;

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default);
}
