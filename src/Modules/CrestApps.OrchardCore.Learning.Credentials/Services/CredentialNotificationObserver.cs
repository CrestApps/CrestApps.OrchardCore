using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Tells the recipient that a credential was awarded or revoked, through the Notifications module with the notification
/// types Credential awarded and Credential revoked, which a user can switch off.
/// </summary>
internal sealed class CredentialNotificationObserver : ICredentialAwardObserver
{
    private readonly ILearningNotificationSender _sender;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialNotificationObserver"/> class.
    /// </summary>
    /// <param name="sender">The Learning notification sender.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CredentialNotificationObserver(ILearningNotificationSender sender, IStringLocalizer<CredentialNotificationObserver> stringLocalizer)
    {
        _sender = sender;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task AwardedAsync(CredentialAward award, CancellationToken cancellationToken = default)
    {
        var name = award?.Achievement?.Name ?? string.Empty;

        Send(award, CredentialsConstants.NotificationType, S["You earned the {0} credential", name], S["You earned the {0} credential. Open My credentials to download it or share it.", name]);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RevokedAsync(CredentialAward award, CancellationToken cancellationToken = default)
    {
        // An award revoked because its person's data was erased tells nobody.
        if (award?.ErasedUtc is not null)
        {
            return Task.CompletedTask;
        }

        var name = award?.Achievement?.Name ?? string.Empty;

        Send(award, CredentialsConstants.RevokedNotificationType, S["Your {0} credential was revoked", name], S["Your {0} credential was revoked. Its verification page now says so. Open My credentials to see your credentials.", name]);

        return Task.CompletedTask;
    }

    private void Send(CredentialAward award, string type, string subject, string body)
    {
        if (string.IsNullOrEmpty(award?.RecipientUserId))
        {
            return;
        }

        _sender.Send(award.RecipientUserId, new LearningNotificationMessage
        {
            Subject = subject,
            Summary = body,
            TextBody = body,
            Type = type,
            ReferenceId = award.ItemId,
            Url = "~/learning/credentials",
        });
    }
}
