using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tickets.Navigation;

/// <summary>
/// Adds the Tickets menu and its settings pages under Configuration.
/// </summary>
internal sealed class TicketsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketsAdminMenu(IStringLocalizer<TicketsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Configuration"], configuration => configuration
                .Add(S["Tickets"], S["Tickets"].PrefixPosition(), settings => settings
                    .Id("ticketSettings")
                    .Add(S["Desks"], S["Desks"].PrefixPosition("1"), desks => desks
                        .Action("Index", "TicketDesks", TicketsConstants.Features.Tickets)
                        .Permission(TicketsPermissions.ManageTicketSettings)
                        .LocalNav())
                    .Add(S["Statuses"], S["Statuses"].PrefixPosition("2"), statuses => statuses
                        .Action("Index", "TicketStatuses", TicketsConstants.Features.Tickets)
                        .Permission(TicketsPermissions.ManageTicketSettings)
                        .LocalNav())
                    .Add(S["Priorities"], S["Priorities"].PrefixPosition("3"), priorities => priorities
                        .Action("Index", "TicketPriorities", TicketsConstants.Features.Tickets)
                        .Permission(TicketsPermissions.ManageTicketSettings)
                        .LocalNav())
                    .Add(S["Views"], S["Views"].PrefixPosition("4"), views => views
                        .Action("Index", "TicketViews", TicketsConstants.Features.Tickets)
                        .Permission(TicketsPermissions.ManageTicketSettings)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
