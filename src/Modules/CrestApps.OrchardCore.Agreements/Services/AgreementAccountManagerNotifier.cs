using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Agreements.Services;

/// <summary>
/// Tells the account's manager (the owner on its account team) when an agreement passes 80% and 100% of its hours, and
/// when it is about to end, through Orchard Core notifications.
/// </summary>
internal sealed class AgreementAccountManagerNotifier : IAgreementObserver
{
    private readonly IContentManager _contentManager;
    private readonly INotificationService _notifications;
    private readonly IUserService _users;
    private readonly IServiceProvider _services;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    public AgreementAccountManagerNotifier(
        IContentManager contentManager,
        INotificationService notifications,
        IUserService users,
        IServiceProvider services,
        IOptions<AdminOptions> adminOptions,
        IStringLocalizer<AgreementAccountManagerNotifier> stringLocalizer)
    {
        _adminOptions = adminOptions.Value;
        _contentManager = contentManager;
        _notifications = notifications;
        _users = users;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task OnExpiringAsync(Agreement agreement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        await NotifyAsync(agreement, AgreementsConstants.NotificationTypes.Ending, account => new NotificationMessage
        {
            Subject = S["Agreement {0} with {1} ends on {2}", agreement.Number, account, agreement.EndDate?.ToString("d", System.Globalization.CultureInfo.CurrentCulture)],
            Summary = S["{0} ends soon; renew it or let it end.", agreement.Name],
            TextBody = S["The agreement {0} {1} with {2} ends on {3}.", agreement.Number, agreement.Name, account, agreement.EndDate?.ToString("d", System.Globalization.CultureInfo.CurrentCulture)],
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task OnHoursUsedAsync(Agreement agreement, int percent, AgreementBalance balance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(balance);

        var used = (balance.UsedMinutes / 60).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
        var covered = (balance.CoveredMinutes / 60).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

        await NotifyAsync(agreement, AgreementsConstants.NotificationTypes.Hours, account => new NotificationMessage
        {
            Subject = percent >= 100
                ? S["{0} has used all the hours of agreement {1}", account, agreement.Number]
                : S["{0} has used {1}% of the hours of agreement {2}", account, percent, agreement.Number],
            Summary = S["{0} of {1} hours used.", used, covered],
            TextBody = percent >= 100
                ? S["{0} has used {1} of the {2} hours of {3} {4}. Work goes on, and the hours past what the agreement covers are billable.", account, used, covered, agreement.Number, agreement.Name]
                : S["{0} has used {1} of the {2} hours of {3} {4}.", account, used, covered, agreement.Number, agreement.Name],
        }, cancellationToken);
    }

    private async Task NotifyAsync(Agreement agreement, string type, Func<string, NotificationMessage> message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(agreement.AccountId)
            || await _contentManager.GetAsync(agreement.AccountId, VersionOptions.Published) is not { } account
            || !account.TryGet<AccountTeamPart>(out var team)
            || string.IsNullOrEmpty(team.OwnerStaffId)
            || _services.GetService<IStaffDirectory>() is not { } staff)
        {
            return;
        }

        var owner = (await staff.GetAsync([team.OwnerStaffId], cancellationToken)).GetValueOrDefault(team.OwnerStaffId);

        if (string.IsNullOrEmpty(owner?.UserId) || await _users.GetUserByUniqueIdAsync(owner.UserId) is not { } user)
        {
            return;
        }

        var notification = message(account.DisplayText);
        notification.Type = type;
        notification.ReferenceId = agreement.ItemId;
        notification.Url = $"~/{_adminOptions.AdminUrlPrefix}/agreements/edit/{agreement.ItemId}";

        await _notifications.SendAsync(user, notification, cancellationToken);
    }
}
