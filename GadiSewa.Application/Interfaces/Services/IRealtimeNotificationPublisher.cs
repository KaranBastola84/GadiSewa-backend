namespace GadiSewa.Application.Interfaces.Services;

public interface IRealtimeNotificationPublisher
{
    Task NotifyAdminsLowStockAsync(
        Guid partId,
        string partName,
        int stockQuantity,
        int threshold,
        CancellationToken cancellationToken = default);

    Task NotifyStaffSaleCreatedAsync(
        Guid salesInvoiceId,
        string invoiceNumber,
        Guid customerId,
        decimal totalAmount,
        CancellationToken cancellationToken = default);
}
