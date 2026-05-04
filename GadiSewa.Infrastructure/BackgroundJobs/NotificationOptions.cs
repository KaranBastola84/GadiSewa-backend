namespace GadiSewa.Infrastructure.BackgroundJobs;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public int OverdueReminderIntervalInDays { get; init; } = 1;
}
