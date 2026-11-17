using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.Agent.Settings;

/// <summary>
/// Lists the site settings pages the caller can open, from the admin menu the caller sees: every settings page is a
/// menu item of the Settings module with the group ID in its route, and the menu already leaves out the pages the
/// caller may not manage.
/// </summary>
public sealed class ListSiteSettingsGroupsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSiteSettingsGroups";

    private const string SettingsArea = "OrchardCore.Settings";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the title, menu path or group ID of a settings page must contain, such as 'email' or 'login'. Leave it out to list every settings page."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of settings pages on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the site settings pages the current user can manage, with group id, title, menu path and link. Call it first to find the group id for getSiteSettings.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AdminPermissions.AccessAdminPanel))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        search = search?.Trim();

        var httpContext = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var groups = (await AdminMenuPages.GetPagesAsync(services, httpContext))
            .Select(menuPage => (Page: menuPage, GroupId: GetGroupId(menuPage)))
            .Where(entry => !string.IsNullOrEmpty(entry.GroupId))
            .GroupBy(entry => entry.GroupId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(entry => string.IsNullOrEmpty(search) ||
                entry.GroupId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                entry.Page.MenuPath.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var items = new List<object>();

        foreach (var (menuPage, groupId) in groups.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var customSettings = await SiteSettingsEditor.IsCustomSettingsGroupAsync(services, groupId);

            items.Add(new
            {
                groupId,
                title = menuPage.Title,
                menuPath = menuPage.MenuPath,
                link = menuPage.Href,
                customSettings = customSettings ? true : (bool?)null,
            });
        }

        return AgentToolResults.Json(new
        {
            items,
            total = groups.Count,
            page,
            pageSize,
            truncated = groups.Count > page * pageSize
                ? true
                : (bool?)null,
            note = "Call getSiteSettings with a group id to read its fields. A page marked customSettings holds a custom settings content type: these tools cannot read or change it; a person edits it on its page.",
        });
    }

    private static string GetGroupId(AdminPage page)
    {
        if (page.RouteValues is null ||
            !page.RouteValues.TryGetValue("area", out var area) ||
            !string.Equals(area?.ToString(), SettingsArea, StringComparison.OrdinalIgnoreCase) ||
            !page.RouteValues.TryGetValue("groupId", out var groupId))
        {
            return null;
        }

        return groupId?.ToString();
    }
}
