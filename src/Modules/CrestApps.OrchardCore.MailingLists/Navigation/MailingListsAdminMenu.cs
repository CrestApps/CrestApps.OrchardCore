using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.MailingLists.Navigation;

/// <summary>
/// Adds the top-level <b>Mailing lists</b> menu and the <b>Settings &gt; Mailing lists</b> entry.
/// </summary>
internal sealed class MailingListsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", MailingListsConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public MailingListsAdminMenu(IStringLocalizer<MailingListsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Mailing lists"], S["Mailing lists"].PrefixPosition("8"), lists => lists
            .Id("mailingLists")
            .AddClass("mailing-lists")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-envelope-open-text")
            .Add(S["Lists"], S["Lists"].PrefixPosition("10"), all => all
                .Id("mailingListsIndex")
                .Action("Index", "MailingLists", MailingListsConstants.Features.MailingLists)
                .Permission(MailingListsPermissions.ViewMailingListMembers)
                .LocalNav()));

        builder.Add(S["Settings"], settings => settings
            .Add(S["Mailing Lists"], S["Mailing Lists"].PrefixPosition(), mailing => mailing
                .Id("mailingListsSettings")
                .Action("Index", "Admin", _settingsRouteValues)
                .Permission(MailingListsPermissions.ManageMailingListSettings)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
