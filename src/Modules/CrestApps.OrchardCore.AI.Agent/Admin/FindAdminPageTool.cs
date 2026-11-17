using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.Agent.Admin;

/// <summary>
/// Searches the admin menu the caller sees, the same menu walk the admin quick navigation palette (Ctrl+K) does, so
/// it finds only the pages the caller may open.
/// </summary>
public sealed class FindAdminPageTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "findAdminPage";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Words the title or menu path of the page must contain, such as 'recaptcha' or 'background tasks'. Every word must match."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page of results to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of admin pages on a page of results. The default is 20 and the most is 50."
        }
      },
      "required": [
        "search"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Searches the admin menu the current user can see and returns matching pages with their menu path and link. Use it to tell the user where something is configured or to give a link to an admin page.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, AdminPermissions.AccessAdminPanel))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("search", out var search) || string.IsNullOrWhiteSpace(search))
        {
            return "Nothing was searched: give the words to look for in the admin menu, such as 'recaptcha'.";
        }

        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var httpContext = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var matches = (await AdminMenuPages.GetPagesAsync(services, httpContext))
            .Where(adminPage => words.All(word => adminPage.MenuPath.Contains(word, StringComparison.OrdinalIgnoreCase)))
            // A page whose own title matches comes before a page found through the menu above it.
            .OrderByDescending(adminPage => words.Count(word => adminPage.Title.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return AgentToolResults.Json(new
        {
            items = matches
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(adminPage => new
                {
                    title = adminPage.Title,
                    menuPath = adminPage.MenuPath,
                    link = adminPage.Href,
                })
                .ToList(),
            total = matches.Count,
            page,
            pageSize,
            truncated = matches.Count > page * pageSize
                ? true
                : (bool?)null,
            note = matches.Count == 0
                ? "No admin page you can open matches. Try other words, or a broader word such as 'settings'."
                : null,
        });
    }
}
