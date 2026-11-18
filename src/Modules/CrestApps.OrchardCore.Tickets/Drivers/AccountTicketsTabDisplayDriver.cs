using CrestApps.Core.Services;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The Tickets tab of an account's page: the account's latest tickets the user may see, how many are still being worked
/// on, and a button to open a ticket for the account.
/// </summary>
internal sealed class AccountTicketsTabDisplayDriver : ContentDisplayDriver
{
    private const int MaxTickets = 25;

    private static readonly TicketStatusCategory[] _open = [TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold];

    private readonly ITicketService _tickets;
    private readonly ITicketAccessService _access;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountTicketsTabDisplayDriver(
        ITicketService tickets,
        ITicketAccessService access,
        IAuthorizationService authorizationService,
        ICatalog<TicketDesk> desks,
        ICatalog<TicketStatus> statuses,
        ICatalog<TicketPriority> priorities,
        IHttpContextAccessor httpContextAccessor)
    {
        _tickets = tickets;
        _access = access;
        _authorizationService = authorizationService;
        _desks = desks;
        _statuses = statuses;
        _priorities = priorities;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin || user is null)
        {
            return null;
        }

        var scope = await _access.ApplyScopeAsync(user, new TicketQuery
        {
            AccountId = contentItem.ContentItemId,
            Sort = TicketSort.Updated,
            Take = MaxTickets,
        });

        if (scope is null)
        {
            return null;
        }

        return Initialize<AccountTicketsTabViewModel>("AccountTicketsTab", async model =>
        {
            var (tickets, total) = await _tickets.ListAsync(scope);
            var (_, open) = await _tickets.ListAsync(new TicketScopedQuery
            {
                Query = new TicketQuery { AccountId = contentItem.ContentItemId, Categories = [.. _open], Take = 1 },
                AllDeskIds = scope.AllDeskIds,
                TeamDeskIds = scope.TeamDeskIds,
                AssignedDeskIds = scope.AssignedDeskIds,
                TeamIds = scope.TeamIds,
                UserId = scope.UserId,
            });
            var statuses = (await _statuses.GetAllAsync()).ToDictionary(status => status.ItemId, StringComparer.Ordinal);
            var priorities = (await _priorities.GetAllAsync()).ToDictionary(priority => priority.ItemId, priority => priority.Name, StringComparer.Ordinal);

            model.AccountId = contentItem.ContentItemId;
            model.Total = total;
            model.Open = open;
            model.Tickets = [.. tickets.Select(ticket =>
            {
                var part = ticket.GetOrCreate<TicketPart>();
                var status = part.StatusId is null ? null : statuses.GetValueOrDefault(part.StatusId);

                return new AccountTicketRow
                {
                    Id = ticket.ContentItemId,
                    Number = part.Number,
                    Subject = ticket.DisplayText,
                    Status = status?.Name,
                    StatusColor = status?.Color,
                    Priority = part.PriorityId is null ? null : priorities.GetValueOrDefault(part.PriorityId),
                    ModifiedUtc = ticket.ModifiedUtc ?? ticket.CreatedUtc,
                };
            })];

            foreach (var desk in await _desks.GetAllAsync())
            {
                if (await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ForDesk(TicketOperation.Reply, desk)))
                {
                    model.CanCreate = true;

                    break;
                }
            }
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Tickets));
    }
}
