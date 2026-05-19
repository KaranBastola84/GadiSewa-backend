namespace GadiSewa.Application.Interfaces.Services;

public interface INotificationService
{
    Task CheckAndNotifyLowStockAsync(IEnumerable<Guid> partIds, CancellationToken cancellationToken = default);
    Task NotifySaleCreatedAsync(
        Guid salesInvoiceId,
        string invoiceNumber,
        Guid customerId,
        decimal totalAmount,
        CancellationToken cancellationToken = default);
}
