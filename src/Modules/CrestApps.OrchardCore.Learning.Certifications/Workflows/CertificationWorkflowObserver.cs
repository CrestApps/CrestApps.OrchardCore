using System.Globalization;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

/// <summary>
/// Triggers the certification path workflow events.
/// </summary>
internal sealed class CertificationWorkflowObserver : ICertificationPathObserver
{
    public Task RenewalOpenedAsync(CertificationPathProgress progress, CertificationPath path, CancellationToken cancellationToken = default)
        => Trigger(nameof(CertificationRenewalOpenedEvent), progress);

    public Task LapsedAsync(CertificationPathProgress progress, CertificationPath path, CancellationToken cancellationToken = default)
        => Trigger(nameof(CertificationLapsedEvent), progress);

    private static Task Trigger(string name, CertificationPathProgress progress)
    {
        var input = new Dictionary<string, object>
        {
            ["ProgressId"] = progress.ItemId,
            ["PathId"] = progress.PathId,
            ["CertificationId"] = progress.CertificationId,
            ["UserId"] = progress.UserId,
            ["Cycle"] = progress.Cycle,
            ["CertificationRecordId"] = progress.CurrentRecordId,
            ["ExpiresOn"] = progress.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, progress.ItemId));

        return Task.CompletedTask;
    }
}
