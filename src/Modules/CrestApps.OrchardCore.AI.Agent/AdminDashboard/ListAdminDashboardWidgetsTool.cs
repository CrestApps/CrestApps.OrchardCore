using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using YesSql;
using YesSql.Services;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.AdminDashboard;

/// <summary>
/// Lists the widgets of the admin dashboard as the dashboard pages show them: a caller who manages the dashboard sees
/// the latest version of the widgets whose type they may edit, as on the Manage Dashboard page, and anyone else sees
/// the published widgets they may view, as on the dashboard.
/// </summary>
public sealed class ListAdminDashboardWidgetsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAdminDashboardWidgets";

    /// <summary>
    /// The stereotype of the content types that can be dashboard widgets.
    /// </summary>
    public const string DashboardWidgetStereotype = "DashboardWidget";

    private const string DashboardPartName = "DashboardPart";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of widgets on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the widgets on the admin dashboard with their content type, position, width and height, and the content types that can be added as widgets. Use it before adding or rearranging widgets.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.AccessAdminDashboard))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var user = AgentToolAuthorization.GetCaller(services);
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var canManage = await authorizationService.AuthorizeAsync(user, OrchardCorePermissions.ManageAdminDashboard);

        var widgetTypes = (await services.GetRequiredService<IContentDefinitionManager>().ListTypeDefinitionsAsync())
            .Where(type => type.StereotypeEquals(DashboardWidgetStereotype))
            .ToDictionary(type => type.Name, StringComparer.Ordinal);

        var typeNames = widgetTypes.Keys.ToArray();
        var session = services.GetRequiredService<YSession>();

        IEnumerable<ContentItem> widgets = typeNames.Length == 0
            ? []
            : canManage
                ? await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(typeNames) && index.Latest).ListAsync(cancellationToken)
                : await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(typeNames) && index.Published).ListAsync(cancellationToken);

        var items = new List<(double Position, object Item)>();

        foreach (var widget in widgets)
        {
            var allowed = canManage
                ? await authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.EditContent, widget.ContentType, userId)
                : await authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, widget);

            if (!allowed)
            {
                continue;
            }

            var part = ((JsonObject)widget.Content)[DashboardPartName] as JsonObject;
            var position = ReadNumber(part, "Position", 0);

            items.Add((position, new
            {
                contentItemId = widget.ContentItemId,
                contentType = widget.ContentType,
                contentTypeDisplayName = widgetTypes[widget.ContentType].DisplayName,
                displayText = widget.DisplayText,
                published = widget.Published,
                position,
                width = ReadNumber(part, "Width", 1),
                height = ReadNumber(part, "Height", 1),
            }));
        }

        var creatable = new List<object>();

        if (canManage)
        {
            foreach (var type in widgetTypes.Values.OrderBy(type => type.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                if (await authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.EditContent, type.Name, userId))
                {
                    creatable.Add(new
                    {
                        contentType = type.Name,
                        displayName = type.DisplayName,
                    });
                }
            }
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var ordered = items.OrderBy(item => item.Position).Select(item => item.Item).ToList();

        return AgentToolResults.Json(new
        {
            items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            total = ordered.Count,
            page,
            pageSize,
            truncated = ordered.Count > page * pageSize
                ? true
                : (bool?)null,
            view = canManage ? "manage" : "dashboard",
            widgetContentTypes = canManage ? creatable : null,
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.AdminDashboard", "Dashboard", canManage ? "Manage" : "Index"),
            note = canManage
                ? "Widgets are content items: create, edit, publish or delete them with the content tools, using one of widgetContentTypes. Position orders the widgets; width and height are the sizes set on the Manage Dashboard page."
                : "These are the published widgets you can view. Managing the dashboard needs the Manage the Admin Dashboard permission.",
        });
    }

    private static double ReadNumber(JsonObject part, string name, double defaultValue)
    {
        if (part?[name] is JsonValue value)
        {
            if (value.TryGetValue<double>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<string>(out var text) &&
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return defaultValue;
    }
}
