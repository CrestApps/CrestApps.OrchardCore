using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Tells the holder when a certificate a certification path earned is issued, renewed or revoked, when it expires soon,
/// when renewal opens and when it lapsed.
/// </summary>
internal sealed class CertificationNotificationObserver : ICertificationRecordObserver, ICertificationExpiryObserver, ICertificationPathObserver
{
    private readonly IServiceProvider _services;
    private readonly ILearningNotificationSender _sender;

    internal readonly IStringLocalizer S;

    public CertificationNotificationObserver(IServiceProvider services, ILearningNotificationSender sender, IStringLocalizer<CertificationNotificationObserver> stringLocalizer)
    {
        _services = services;
        _sender = sender;
        S = stringLocalizer;
    }

    public Task IssuedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => NotifyHolderAsync(context.Record, CertificationsConstants.NotificationTypes.Issued,
            S["Your {0} certificate is issued", context.Certification?.Name ?? string.Empty],
            S["Your {0} certificate is issued. Open My certifications to see it and its verification page.", context.Certification?.Name ?? string.Empty]);

    public Task RenewedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => NotifyHolderAsync(context.Record, CertificationsConstants.NotificationTypes.Issued,
            S["Your {0} certificate is renewed", context.Certification?.Name ?? string.Empty],
            S["Your {0} certificate is renewed. Open My certifications to see the new certificate.", context.Certification?.Name ?? string.Empty]);

    public Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => NotifyHolderAsync(context.Record, CertificationsConstants.NotificationTypes.Revoked,
            S["Your {0} certificate was revoked", context.Certification?.Name ?? string.Empty],
            S["Your {0} certificate was revoked. Open My certifications to see your certificates.", context.Certification?.Name ?? string.Empty]);

    public Task ExpiringAsync(CertificationRecord record, Certification certification, int daysBefore, CancellationToken cancellationToken = default)
        => NotifyHolderAsync(record, CertificationsConstants.NotificationTypes.Expiring,
            S["Your {0} certificate expires in {1} days", certification?.Name ?? string.Empty, daysBefore],
            S["Your {0} certificate expires in {1} days. Open My certifications to see how to renew it.", certification?.Name ?? string.Empty, daysBefore]);

    public Task ExpiredAsync(CertificationRecord record, Certification certification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RenewalOpenedAsync(CertificationPathProgress progress, CertificationPath path, CancellationToken cancellationToken = default)
        => NotifyAsync(progress.UserId, progress.ItemId, CertificationsConstants.NotificationTypes.RenewalOpened,
            S["Renew your certificate"],
            S["The renewal of your certificate is open. Open My certifications to see what renews it."]);

    public Task LapsedAsync(CertificationPathProgress progress, CertificationPath path, CancellationToken cancellationToken = default)
        => NotifyAsync(progress.UserId, progress.ItemId, CertificationsConstants.NotificationTypes.Lapsed,
            S["Your certificate lapsed"],
            path.GracePeriodDays > 0
                ? S["Your certificate expired without a renewal. You can still renew it for {0} days.", path.GracePeriodDays]
                : S["Your certificate expired without a renewal."]);

    private async Task NotifyHolderAsync(CertificationRecord record, string type, string subject, string body)
    {
        if (record.Source != CertificationsConstants.Source)
        {
            return;
        }

        // The holder may be a staff record; the progress of the certificate names the user.
        foreach (var progress in await _services.GetRequiredService<CertificationPathProgressStore>().ListByRecordAsync(record.ItemId))
        {
            await NotifyAsync(progress.UserId, record.ItemId, type, subject, body);
        }
    }

    private Task NotifyAsync(string userId, string referenceId, string type, string subject, string body)
    {
        _sender.Send(userId, new LearningNotificationMessage
        {
            Subject = subject,
            Summary = body,
            TextBody = body,
            Type = type,
            ReferenceId = referenceId,
            Url = "~/learning/certifications",
        });

        return Task.CompletedTask;
    }
}
