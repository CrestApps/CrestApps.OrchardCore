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
        // The list is for whoever may view the tickets of at least one desk; desk permissions are dynamic, so a menu
        // permission can't say it.
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null && (await _access.GetViewScopesAsync(httpContext.User, httpContext.RequestAborted)).Count > 0)
        {
            builder.Add(S["Tickets"], S["Tickets"].PrefixPosition("6"), tickets => tickets
                .AddClass("tickets")
                .AddClass("icon-class-fa-solid")
                .AddClass("icon-class-fa-ticket")
                .Id("tickets")
                .Action("Index", "Tickets", TicketsConstants.Features.Tickets)
                .LocalNav());
        }

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
                        .LocalNav())
                    .Add(S["E-mails"], S["E-mails"].PrefixPosition("5"), emails => emails
                        .Action("Index", "Admin", _emailRouteValues)
                        .Permission(TicketsPermissions.ManageTicketSettings)
                        .LocalNav())));

    }
}
