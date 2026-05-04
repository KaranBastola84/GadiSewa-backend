namespace GadiSewa.Application.Interfaces.Services;

public interface IEmailService
{
    Task SendEmailVerificationEmailAsync(string email, string fullName, string verificationToken, CancellationToken cancellationToken = default);

    Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(string email, string fullName, string resetToken, CancellationToken cancellationToken = default);

    Task SendLowStockAlertAsync(string toEmail, string partName, int stockQuantity, CancellationToken cancellationToken = default);

    Task SendSalesInvoiceEmailAsync(string toEmail, string customerName, string invoiceNumber, string invoiceHtml, CancellationToken cancellationToken = default);

    Task SendOverdueReminderEmailAsync(string toEmail, string customerName, string invoiceNumber, decimal amountDue, DateTimeOffset dueDate, CancellationToken cancellationToken = default);
}
