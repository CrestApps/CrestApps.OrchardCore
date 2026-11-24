using System.Globalization;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Skills.Webhooks;

/// <summary>
/// Publishes the certificates the site issues, renews and revokes as webhook events.
/// </summary>
internal sealed class SkillsWebhookObserver : ICertificationRecordObserver
{
    public const string CertificateIssued = "skills.certificate.issued";

    public const string CertificateRenewed = "skills.certificate.renewed";

    public const string CertificateRevoked = "skills.certificate.revoked";

    public Task IssuedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => Publish(CertificateIssued, context.Record.ItemId + ":issued", context);

    public Task RenewedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => Publish(CertificateRenewed, context.Record.ItemId + ":renewed", context);

    public Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => Publish(CertificateRevoked, context.Record.ItemId + ":revoked", context);

    private static Task Publish(string eventName, string eventId, CertificationRecordContext context)
    {
        var record = context.Record;

        // The verification code is left out: it is the holder's proof, and a webhook receiver has the record id.
        var payload = new
        {
            certificateId = record.ItemId,
            certificationId = record.CertificationId,
            certificationName = context.Certification?.Name,
            subjectKind = record.SubjectKind,
            subjectId = record.SubjectId,
            number = record.Number,
            issuedOn = record.IssuedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            expiresOn = record.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            status = record.Status.ToString(),
            source = record.Source,
            sourceId = record.SourceId,
            previousCertificateId = record.PreviousRecordId,
            revokedReason = record.RevokedReason,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWebhookPublisher>().PublishAsync(eventName, payload, eventId));

        return Task.CompletedTask;
    }
}
