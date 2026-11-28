using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Deletes an event category, as the Categories page does.
/// </summary>
internal sealed class DeleteCalendarCategoryTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "deleteCalendarCategory";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "categoryId": { "type": "string", "description": "The category's ID, from listCalendarCategories." }
      },
      "required": ["categoryId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Deletes an event category. Events that used it keep their own color and lose the category.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var categoryService = services.GetRequiredService<ICalendarCategoryService>();
        var categoryId = Read(arguments, "categoryId");
        var category = categoryId is null ? null : await categoryService.FindByIdAsync(categoryId, cancellationToken);

        if (category is null)
        {
            return "There is no category with that ID. Use listCalendarCategories to find it.";
        }

        var canManageShared = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, CalendarPermissions.ManageAllCalendars);

        if (!CalendarCategoryRules.CanChange(category, CalendarUser.GetId(user), canManageShared))
        {
            return category.IsShared
                ? "Only a calendar administrator can delete a shared category."
                : "The user can delete only their own categories.";
        }

        await categoryService.DeleteAsync(category, cancellationToken);

        return CalendarToolSupport.Json(new { deleted = true, id = category.ItemId, name = category.Name });
    }
}
