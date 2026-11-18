using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// Adds the requester's requests to My account.
/// </summary>
internal sealed class TicketsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TicketsAccountMenu(IStringLocalizer<TicketsAccountMenu> stringLocalizer)
        : base(UsersConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Requests"], "40", requests => requests
            .Id("account-tickets")
            .AddClass("fa-solid fa-life-ring")
            .Action(nameof(TicketPortalController.Index), "TicketPortal", new { area = TicketsConstants.Features.Tickets }));

        return ValueTask.CompletedTask;
    }
}
