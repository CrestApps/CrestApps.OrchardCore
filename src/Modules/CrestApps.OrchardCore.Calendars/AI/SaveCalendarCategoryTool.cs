using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Creates or changes an event category, with the rules of the Categories page.
/// </summary>
internal sealed class SaveCalendarCategoryTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "saveCalendarCategory";

    /// <summary>
    /// The color a new category gets when none is given, as on the Categories page.
    /// </summary>
    public const string DefaultColor = "#1971c2";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "categoryId": { "type": "string", "description": "The category to change. Leave out to create a category." },
        "name": { "type": "string", "description": "The name. Required for a new category." },
        "color": { "type": "string", "description": "The color, #rrggbb. A new category is blue (#1971c2) when it is left out." },
        "shared": { "type": "boolean", "description": "Whether everyone can use the category. Only a calendar administrator may share a category. Left out, a category keeps its sharing and a new one is the user's own." }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Creates or updates an event category: name, color (#rrggbb) and whether everyone shares it. Fields left out keep their value.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var categoryService = services.GetRequiredService<ICalendarCategoryService>();
        var userId = CalendarUser.GetId(user);
        var canShare = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, CalendarPermissions.ManageAllCalendars);
        var categoryId = Read(arguments, "categoryId");
        CalendarCategory category;

        if (categoryId is null)
        {
            category = new CalendarCategory();
        }
        else
        {
            category = await categoryService.FindByIdAsync(categoryId, cancellationToken);

            if (category is null)
            {
                return "There is no category with that ID. Use listCalendarCategories to find it.";
            }

            if (!CalendarCategoryRules.CanChange(category, userId, canShare))
            {
                return category.IsShared
                    ? "Only a calendar administrator can change a shared category."
                    : "The user can change only their own categories.";
            }
        }

        var name = CalendarToolSupport.ReadRaw(arguments, "name") ?? category.Name;
        var color = Read(arguments, "color") ?? category.Color ?? DefaultColor;
        var shared = CalendarToolSupport.ReadBool(arguments, "shared");

        if (CalendarToolSupport.Has(arguments, "shared") && shared is null)
        {
            return "shared must be true or false.";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "The name is required.";
        }

        if (!CalendarCategoryRules.IsValidColor(color))
        {
            return "Choose a #rrggbb color, such as #1971c2.";
        }

        if (shared == true && !canShare)
        {
            return "Only a calendar administrator can share a category with everyone.";
        }

        CalendarCategoryRules.Apply(category, name, color, shared ?? (categoryId is not null && category.IsShared), canShare, userId);

        await categoryService.SaveAsync(category, cancellationToken);

        return CalendarToolSupport.Json(new
        {
            created = categoryId is null,
            id = category.ItemId,
            name = category.Name,
            color = category.Color,
            shared = category.IsShared,
        });
    }
}
