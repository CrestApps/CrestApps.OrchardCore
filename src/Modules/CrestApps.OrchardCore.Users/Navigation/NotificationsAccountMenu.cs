using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Users.Navigation;

/// <summary>
/// Adds Notifications, the site's Notifications page, to the account menu.
/// </summary>
public sealed class NotificationsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public NotificationsAccountMenu(IStringLocalizer<NotificationsAccountMenu> stringLocalizer)
        : base(UsersConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Notifications"], "90", notifications => notifications
            .Id("account-notifications")
            .AddClass("fa-regular fa-bell")
            .Action("Index", "UserNotifications", new { area = UsersConstants.Feature.Area })
            .Permission(NotificationPermissions.ManageNotifications));

        return ValueTask.CompletedTask;
    }
}
