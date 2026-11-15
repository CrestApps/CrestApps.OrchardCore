using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tickets.Navigation;

/// <summary>
/// Adds the Tickets menu and its settings pages under Configuration.
/// </summary>
internal sealed class TicketsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _emailRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", Drivers.TicketEmailSettingsDisplayDriver.GroupId },
    };

    private readonly ITicketAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketsAdminMenu"/> class.
    /// </summary>
    /// <param name="access">The ticket access service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketsAdminMenu(ITicketAccessService access, IHttpContextAccessor httpContextAccessor, IStringLocalizer<TicketsAdminMenu> stringLocalizer)
    {
        _access = access;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        // One Tickets menu: the list, for whoever may view the tickets of at least one desk (desk permissions are dynamic,
        // so a menu permission can't say it), and the help desk's own setup for whoever manages it. A user with neither
        // sees no Tickets menu.
        var httpContext = _httpContextAccessor.HttpContext;
        var canViewTickets = httpContext is not null && (await _access.GetViewScopesAsync(httpContext.User, httpContext.RequestAborted)).Count > 0;

        builder.Add(S["Tickets"], S["Tickets"].PrefixPosition("6"), tickets =>
        {
            tickets
                .AddClass("tickets")
                .AddClass("icon-class-fa-solid")
                .AddClass("icon-class-fa-ticket")
                .Id("tickets");

            if (canViewTickets)
            {
                tickets.Add(S["Tickets"], S["Tickets"].PrefixPosition("1"), list => list
                    .Id("ticketList")
                    .Action("Index", "Tickets", TicketsConstants.Features.Tickets)
                    .LocalNav());
            }

            tickets
                .Add(S["Desks"], S["Desks"].PrefixPosition("2"), desks => desks
                    .Id("ticketDesks")
                    .Action("Index", "TicketDesks", TicketsConstants.Features.Tickets)
                    .Permission(TicketsPermissions.ManageTicketSettings)
                    .LocalNav())
                .Add(S["Statuses"], S["Statuses"].PrefixPosition("3"), statuses => statuses
                    .Id("ticketStatuses")
                    .Action("Index", "TicketStatuses", TicketsConstants.Features.Tickets)
                    .Permission(TicketsPermissions.ManageTicketSettings)
                    .LocalNav())
                .Add(S["Priorities"], S["Priorities"].PrefixPosition("4"), priorities => priorities
                    .Id("ticketPriorities")
                    .Action("Index", "TicketPriorities", TicketsConstants.Features.Tickets)
                    .Permission(TicketsPermissions.ManageTicketSettings)
                    .LocalNav())
                .Add(S["Views"], S["Views"].PrefixPosition("5"), views => views
                    .Id("ticketViews")
                    .Action("Index", "TicketViews", TicketsConstants.Features.Tickets)
                    .Permission(TicketsPermissions.ManageTicketSettings)
                    .LocalNav())
                .Add(S["E-mails"], S["E-mails"].PrefixPosition("6"), emails => emails
                    .Id("ticketEmails")
                    .Action("Index", "Admin", _emailRouteValues)
                    .Permission(TicketsPermissions.ManageTicketSettings)
                    .LocalNav());
        });

    }
}
