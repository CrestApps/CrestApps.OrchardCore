using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Core.Telemetry;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The portal's notifier: the inbox always, and every other channel the worker hasn't turned off, unless they turned
/// the notification's kind off or it is their quiet hours. A channel that fails doesn't stop the others.
/// </summary>
internal sealed class DefaultWorkforceNotifier : IWorkforceNotifier
{
    private readonly StaffMemberStore _workerStore;
    private readonly IEnumerable<IWorkforceNotificationChannel> _channels;
    private readonly IWorkerTimeZoneService _timeZones;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultWorkforceNotifier(StaffMemberStore workerStore, IEnumerable<IWorkforceNotificationChannel> channels, IWorkerTimeZoneService timeZones, IClock clock, ILogger<DefaultWorkforceNotifier> logger)
    {
        _workerStore = workerStore;
        _channels = channels;
        _timeZones = timeZones;
        _clock = clock;
        _logger = logger;
    }

    public async Task NotifyAsync(WorkforceNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var worker = string.IsNullOrEmpty(notification.WorkerId) ? null : await _workerStore.FindByIdAsync(notification.WorkerId, cancellationToken);

        if (worker is null || worker.Status == StaffStatus.Terminated)
        {
            return;
        }

        var settings = worker.TryGet<WorkerPortalSettings>(out var stored) ? stored : new WorkerPortalSettings();
        var kindOff = settings.DisabledKinds.Contains(notification.Kind);

        if (!kindOff && settings.QuietStart.HasValue && settings.QuietEnd.HasValue)
        {
            var zone = await _timeZones.GetAsync(worker, worker.PrimaryTeamId, cancellationToken);

            kindOff = settings.IsQuiet(TimeOnly.FromDateTime(CalendarTime.ToLocal(_clock.UtcNow, zone)));
        }

        var kind = notification.Kind.ToString();

        foreach (var channel in _channels)
        {
            if (channel.CanBeTurnedOff && (kindOff || settings.DisabledChannels.Contains(channel.Name)))
            {
                WorkforceTelemetry.RecordNotification(channel.Name, kind, WorkforceTelemetry.NotificationSkipped);

                continue;
            }

            try
            {
                await channel.SendAsync(worker, notification, cancellationToken);
                WorkforceTelemetry.RecordNotification(channel.Name, kind, WorkforceTelemetry.NotificationSent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                WorkforceTelemetry.RecordNotification(channel.Name, kind, WorkforceTelemetry.NotificationFailed);
                _logger.LogWarning(ex, "A workforce notification could not be sent through the {Channel} channel.", channel.Name);
            }
        }
    }
}
