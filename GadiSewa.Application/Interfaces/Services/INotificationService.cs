namespace GadiSewa.Application.Interfaces.Services;

public interface INotificationService
{
    Task CheckAndNotifyLowStockAsync(IEnumerable<Guid> partIds, CancellationToken cancellationToken = default);
}
