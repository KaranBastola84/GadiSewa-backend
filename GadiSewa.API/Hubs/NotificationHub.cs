using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GadiSewa.API.Hubs;

[Authorize(Roles = "Admin,Staff")]
public sealed class NotificationHub : Hub
{
    public const string HubRoute = "/hubs/notifications";
    public const string AdminGroup = "admins";
    public const string StaffGroup = "staff";
    public const string LowStockAlertEvent = "LowStockAlert";
    public const string SaleCreatedEvent = "SaleCreated";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            await base.OnConnectedAsync();
            return;
        }

        if (Context.User.IsInRole(UserRole.Admin.ToString()))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        }

        if (Context.User.IsInRole(UserRole.Staff.ToString()))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        }

        await base.OnConnectedAsync();
    }
}
