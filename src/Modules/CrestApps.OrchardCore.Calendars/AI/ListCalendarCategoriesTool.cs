using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Lists the event categories the person can use: their own and the shared ones, as the Categories page does.
/// </summary>
internal sealed class ListCalendarCategoriesTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "listCalendarCategories";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "search": { "type": "string", "description": "Text the category name must contain." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Categories per page, 20 by default." }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the event categories the user can use (their own and the shared ones) with id, name and color. Use it to find a category id for an event.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = CalendarUser.GetId(user);
        var search = Read(arguments, "search");
        var (page, pageSize) = CalendarToolSupport.ReadPaging(arguments);
        var canManageShared = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, CalendarPermissions.ManageAllCalendars);
        var categories = (await services.GetRequiredService<ICalendarCategoryService>().ListAsync(userId, cancellationToken))
            .Where(category => search is null || category.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        var items = categories.Skip((page - 1) * pageSize).Take(pageSize).Select(category => new
        {
            id = category.ItemId,
            name = category.Name,
            color = category.Color,
            shared = category.IsShared,
            canChange = CalendarCategoryRules.CanChange(category, userId, canManageShared),
        }).ToList();

        return CalendarToolSupport.Json(new
        {
            items,
            total = categories.Count,
            page,
            pageSize,
            truncated = page * pageSize < categories.Count,
        });
    }
}
