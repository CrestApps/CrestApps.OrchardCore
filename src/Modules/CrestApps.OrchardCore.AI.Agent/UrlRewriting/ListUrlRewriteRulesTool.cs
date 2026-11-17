using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.UrlRewriting;

namespace CrestApps.OrchardCore.AI.Agent.UrlRewriting;

/// <summary>
/// Lists the URL rewriting rules from the rule manager, in the order they are applied, as the URL Rewriting page does.
/// </summary>
public sealed class ListUrlRewriteRulesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listUrlRewriteRules";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the rule name, pattern or substitution must contain. Leave it out to list every rule."
        },
        "type": {
          "type": "string",
          "enum": [
            "Redirect",
            "Rewrite"
          ],
          "description": "Keeps only the redirect rules or only the rewrite rules. Leave it out to keep both."
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
          "description": "The number of rules on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the URL redirect and rewrite rules in the order they are applied, with their patterns and options. Call it before adding a rule so it does not conflict with an existing one.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, UrlRewritingPermissions.ManageUrlRewritingRules))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("type", out var type);
        search = search?.Trim();
        type = type?.Trim();

        var localClock = services.GetRequiredService<ILocalClock>();

        // The rules in the order they are applied, with the changes made earlier in this scope; the position counts
        // every rule.
        var rules = (await UrlRewriteRuleScopeChanges.Get(services).GetAllAsync(services.GetRequiredService<IRewriteRulesManager>()))
            .Select((rule, index) => (Rule: rule, Position: index + 1))
            .Where(entry => string.IsNullOrEmpty(type) || string.Equals(entry.Rule.Source, type, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrEmpty(search) || UrlRewriteRuleToolSupport.Matches(entry.Rule, search))
            .ToList();

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var items = new List<object>();

        foreach (var (rule, position) in rules.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(UrlRewriteRuleToolSupport.Describe(rule, position, await AgentToolResults.ToLocalAsync(localClock, rule.CreatedUtc)));
        }

        return AgentToolResults.Json(new
        {
            items,
            total = rules.Count,
            page,
            pageSize,
            truncated = rules.Count > page * pageSize
                ? true
                : (bool?)null,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.UrlRewriting", "Admin", "Index"),
            note = "Rules are applied from position 1 down. Pages under the admin prefix are never rewritten. Change the order by dragging the rules on the URL Rewriting page.",
        });
    }
}
