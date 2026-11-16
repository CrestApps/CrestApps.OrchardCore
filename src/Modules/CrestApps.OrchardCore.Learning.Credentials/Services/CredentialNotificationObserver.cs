using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Notifications;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Tells the recipient that a credential was awarded, through the Notifications module with the notification type
/// Credential awarded, which a user can switch off.
/// </summary>
internal sealed class CredentialNotificationObserver : ICredentialAwardObserver
{
    private readonly ShellSettings _shellSettings;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialNotificationObserver"/> class.
    /// </summary>
    /// <param name="shellSettings">The tenant settings, for the address prefix.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CredentialNotificationObserver(ShellSettings shellSettings, IStringLocalizer<CredentialNotificationObserver> stringLocalizer)
    {
        _shellSettings = shellSettings;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task AwardedAsync(CredentialAward award, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(award?.RecipientUserId))
        {
            return Task.CompletedTask;
        }

        var prefix = string.IsNullOrEmpty(_shellSettings.RequestUrlPrefix) ? string.Empty : "/" + _shellSettings.RequestUrlPrefix;
        var name = award.Achievement?.Name ?? string.Empty;
        var message = new LearningNotificationMessage
        {
            Subject = S["You earned the {0} credential", name],
            Summary = S["You earned the {0} credential. Open My credentials to download it or share it.", name],
            TextBody = S["You earned the {0} credential. Open My credentials to download it or share it.", name],
            Type = CredentialsConstants.NotificationType,
            ReferenceId = award.ItemId,
            Url = $"{prefix}/learning/credentials",
        };
        var userId = award.RecipientUserId;

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var user = await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);

            if (user is not null)
            {
                await services.GetRequiredService<INotificationService>().SendAsync(user, message);
            }
        });

        return Task.CompletedTask;
    }
}
