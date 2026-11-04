using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The notifier without the portal: it tells nobody.
/// </summary>
internal sealed class NullWorkforceNotifier : IWorkforceNotifier
{
    public Task NotifyAsync(WorkforceNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
