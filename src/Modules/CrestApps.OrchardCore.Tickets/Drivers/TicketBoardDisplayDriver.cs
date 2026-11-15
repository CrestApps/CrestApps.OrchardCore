using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The row and the editor of a ticket board. The editor sets who the board is for, which tickets it shows and its
/// columns, each with one or more statuses; a status the site no longer has is dropped from its column.
/// </summary>
internal sealed class TicketBoardDisplayDriver : DisplayDriver<TicketBoard>
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ITicketBoardService _boards;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketBoardDisplayDriver"/> class.
    /// </summary>
    public TicketBoardDisplayDriver(
        ICatalog<TicketDesk> desks,
        ICatalog<TicketStatus> statuses,
        IContentDefinitionManager contentDefinitionManager,
        ITicketBoardService boards,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<TicketBoardDisplayDriver> stringLocalizer)
    {
        _desks = desks;
        _statuses = statuses;
        _contentDefinitionManager = contentDefinitionManager;
        _boards = boards;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(TicketBoard board, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = board.Name?.ToLowerInvariant() ?? string.Empty;
        }

        var canManage = User is { } user && await _boards.CanManageAsync(user, board);

        var results = new List<IDisplayResult>
        {
            View("TicketBoard_Fields_SummaryAdmin", board)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("TicketBoard_Shared_SummaryAdmin", board)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Shared:5"),
            View("TicketBoard_Columns_SummaryAdmin", board)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Columns:5"),
            Initialize<TicketBoardRowViewModel>("TicketBoard_Buttons_SummaryAdmin", model =>
            {
                model.Board = board;
                model.CanManage = canManage;
            }).Location("Actions:5"),
        };

        return Combine(results);
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketBoard board, BuildEditorContext context)
        => Initialize<TicketBoardViewModel>("TicketBoardFields_Edit", async model =>
        {
            var query = board.Query ?? new TicketQuery();
            var statuses = await OrderedStatusesAsync();

            model.Name = board.Name;
            model.Description = board.Description;
            model.Order = board.Order;
            model.SharedWithTeamId = board.SharedWithTeamId;
            model.CanShare = User is { } user && await _boards.CanShareAsync(user);
            model.Sharing = !string.IsNullOrEmpty(board.OwnerId) || (context.IsNew && !model.CanShare)
                ? TicketBoardSharing.Personal
                : string.IsNullOrEmpty(board.SharedWithTeamId) ? TicketBoardSharing.Everybody : TicketBoardSharing.Team;
            model.DeskIds = [.. query.DeskIds];
            model.TeamIds = [.. query.TeamIds];
            model.ContentTypes = [.. query.ContentTypes];
            model.Assignee = query.Assignee;
            model.Sort = query.Sort;
            model.Statuses = statuses;
            model.Desks = [.. (await _desks.GetAllAsync()).OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase).Select(desk => new SelectListItem(desk.Name, desk.ItemId, query.DeskIds.Contains(desk.ItemId)))];
            model.Types = [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(type => type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
                .OrderBy(type => type.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(type => new SelectListItem(type.DisplayName, type.Name, query.ContentTypes.Contains(type.Name)))];

            // A new board starts with a column per status category, which the owner then shapes.
            var columns = board.Columns is { Count: > 0 } || !context.IsNew
                ? board.Columns ?? []
                : TicketBoardDefaults.Columns(statuses, CategoryName);

            model.Columns = [.. columns.Select(column => new TicketBoardColumnViewModel
            {
                Id = column.Id,
                Name = column.Name,
                StatusIds = [.. column.StatusIds ?? []],
                DefaultStatusId = column.DefaultStatusId,
                Limit = column.Limit,
            })];
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TicketBoard board, UpdateEditorContext context)
    {
        var model = new TicketBoardViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var user = User;
        var statusIds = (await _statuses.GetAllAsync()).Select(status => status.ItemId).ToHashSet(StringComparer.Ordinal);

        board.Name = model.Name?.Trim();
        board.Description = model.Description;
        board.Order = model.Order;

        // Whether the user may share the board is checked by the controller on the board as saved (it must be one they
        // may manage). A team share without a team (the page requires one) keeps the board personal: never wider.
        var sharing = model.Sharing == TicketBoardSharing.Team && string.IsNullOrWhiteSpace(model.SharedWithTeamId)
            ? TicketBoardSharing.Personal
            : model.Sharing;

        switch (sharing)
        {
            case TicketBoardSharing.Team:
                board.OwnerId = null;
                board.SharedWithTeamId = model.SharedWithTeamId;
                break;
            case TicketBoardSharing.Everybody:
                board.OwnerId = null;
                board.SharedWithTeamId = null;
                break;
            default:
                board.OwnerId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
                board.SharedWithTeamId = null;
                break;
        }

        board.Query = new TicketQuery
        {
            DeskIds = [.. Clean(model.DeskIds)],
            TeamIds = [.. Clean(model.TeamIds)],
            ContentTypes = [.. Clean(model.ContentTypes)],
            Assignee = string.IsNullOrWhiteSpace(model.Assignee) ? null : model.Assignee,
            Sort = model.Sort,
        };

        // A blank row (no name, no status) is a column the owner added and left empty; it is dropped.
        board.Columns = [.. (model.Columns ?? [])
            .Where(column => column is not null && (!string.IsNullOrWhiteSpace(column.Name) || (column.StatusIds?.Any(id => !string.IsNullOrWhiteSpace(id)) ?? false)))
            .Select(column => new TicketBoardColumn
            {
                Id = column.Id,
                Name = column.Name?.Trim(),
                StatusIds = [.. Clean(column.StatusIds).Where(statusIds.Contains)],
                DefaultStatusId = string.IsNullOrWhiteSpace(column.DefaultStatusId) || !statusIds.Contains(column.DefaultStatusId) ? null : column.DefaultStatusId,
                Limit = column.Limit,
            })];

        return Edit(board, context);
    }

    private ClaimsPrincipal User
        => _httpContextAccessor.HttpContext?.User;

    private async Task<List<TicketStatus>> OrderedStatusesAsync()
        => [.. (await _statuses.GetAllAsync()).OrderBy(status => status.Order).ThenBy(status => status.Name, StringComparer.CurrentCultureIgnoreCase)];

    private string CategoryName(TicketStatusCategory category)
        => category switch
        {
            TicketStatusCategory.New => S["New"],
            TicketStatusCategory.Open => S["Open"],
            TicketStatusCategory.Pending => S["Pending"],
            TicketStatusCategory.OnHold => S["On hold"],
            TicketStatusCategory.Solved => S["Solved"],
            _ => S["Closed"],
        };

    private static IEnumerable<string> Clean(IEnumerable<string> values)
        => (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal);
}

