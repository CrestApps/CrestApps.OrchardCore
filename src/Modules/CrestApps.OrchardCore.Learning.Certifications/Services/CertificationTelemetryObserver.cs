using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Telemetry;
using CrestApps.OrchardCore.Skills.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Counts certification path changes on the Learning meter.
/// </summary>
internal sealed class CertificationTelemetryObserver : ICertificationRecordObserver, ICertificationPathObserver
{
    public Task IssuedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => Count(context, "issued");

    public Task RenewedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => Count(context, "renewed");

    public Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => Count(context, "revoked");

    public Task RenewalOpenedAsync(CertificationPathProgress progress, CertificationPath path, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.CertificationChanges, "change", "renewal_opened");

        return Task.CompletedTask;
    }

    public Task LapsedAsync(CertificationPathProgress progress, CertificationPath path, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.CertificationChanges, "change", "lapsed");

        return Task.CompletedTask;
    }

    private static Task Count(CertificationRecordContext context, string change)
    {
        if (context.Record.Source == CertificationsConstants.Source)
        {
            LearningTelemetry.Add(LearningTelemetry.CertificationChanges, "change", change);
        }

        return Task.CompletedTask;
    }
}
