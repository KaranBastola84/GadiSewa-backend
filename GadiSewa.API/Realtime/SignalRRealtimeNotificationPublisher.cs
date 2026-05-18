using GadiSewa.API.Hubs;
using GadiSewa.Application.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace GadiSewa.API.Realtime;

public sealed class SignalRRealtimeNotificationPublisher : IRealtimeNotificationPublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRRealtimeNotificationPublisher(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAdminsLowStockAsync(
        Guid partId,
        string partName,
        int stockQuantity,
        int threshold,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            PartId = partId,
            PartName = partName,
            StockQuantity = stockQuantity,
            Threshold = threshold,
            AlertedAt = DateTimeOffset.UtcNow
        };

        return _hubContext.Clients
            .Group(NotificationHub.AdminGroup)
            .SendAsync(NotificationHub.LowStockAlertEvent, payload, cancellationToken);
    }

    public Task NotifyStaffSaleCreatedAsync(
        Guid salesInvoiceId,
        string invoiceNumber,
        Guid customerId,
        decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            SalesInvoiceId = salesInvoiceId,
            InvoiceNumber = invoiceNumber,
            CustomerId = customerId,
            TotalAmount = totalAmount,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return _hubContext.Clients
            .Group(NotificationHub.StaffGroup)
            .SendAsync(NotificationHub.SaleCreatedEvent, payload, cancellationToken);
    }
}
