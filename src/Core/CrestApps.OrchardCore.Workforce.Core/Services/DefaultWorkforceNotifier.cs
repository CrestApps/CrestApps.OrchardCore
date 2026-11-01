using CrestApps.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The portal's notifier: the inbox always, and every other channel the worker hasn't turned off, unless they turned
/// the notification's kind off. A channel that fails doesn't stop the others.
/// </summary>
internal sealed class DefaultWorkforceNotifier : IWorkforceNotifier
{
    private readonly WorkerStore _workerStore;
    private readonly IEnumerable<IWorkforceNotificationChannel> _channels;
    private readonly ILogger _logger;

    public DefaultWorkforceNotifier(WorkerStore workerStore, IEnumerable<IWorkforceNotificationChannel> channels, ILogger<DefaultWorkforceNotifier> logger)
    {
        _workerStore = workerStore;
        _channels = channels;
        _logger = logger;
    }

    public async Task NotifyAsync(WorkforceNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var worker = string.IsNullOrEmpty(notification.WorkerId) ? null : await _workerStore.FindByIdAsync(notification.WorkerId, cancellationToken);

        if (worker is null || worker.Status == WorkerStatus.Terminated)
        {
            return;
        }

        var settings = worker.TryGet<WorkerPortalSettings>(out var stored) ? stored : new WorkerPortalSettings();
        var kindOff = settings.DisabledKinds.Contains(notification.Kind);

        foreach (var channel in _channels)
        {
            if (channel.CanBeTurnedOff && (kindOff || settings.DisabledChannels.Contains(channel.Name)))
            {
                continue;
            }

            try
            {
                await channel.SendAsync(worker, notification, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "A workforce notification could not be sent through the {Channel} channel.", channel.Name);
            }
        }
    }
}
