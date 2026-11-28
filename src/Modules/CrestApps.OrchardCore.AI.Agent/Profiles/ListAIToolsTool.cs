using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Lists what can be attached to an AI profile: the registered tools, the tool instances and the agent profiles. Like
/// the profile editor, it only lists the tools and tool instances the caller may use.
/// </summary>
public sealed class ListAIToolsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAITools";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "kind": {
          "type": "string",
          "enum": ["tool", "toolInstance", "agent"],
          "description": "Only list this kind: registered tools, tool instances or agent profiles. Leave it out to list all three."
        },
        "category": {
          "type": "string",
          "description": "Only list registered tools of this category, for example Content or Users."
        },
        "search": {
          "type": "string",
          "description": "Only list items whose name, title or description contains this text."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many items a page holds, 20 when left out and at most 50."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists what can be attached to an AI profile: registered tools by category (with whether they ask for approval and whether they are safe for visitors who are not signed in), tool instances, and agent profiles. Only tools and tool instances the current user may attach are returned.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIProfiles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("kind", out var kind);
        arguments.TryGetFirstString("category", out var category);
        arguments.TryGetFirstString("search", out var search);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        if (!string.IsNullOrWhiteSpace(kind) && kind is not ("tool" or "toolInstance" or "agent"))
        {
            return $"Nothing was listed: '{kind}' is not a kind. Use tool, toolInstance or agent.";
        }

        var caller = AgentToolAuthorization.GetCaller(services);
        var evaluator = services.GetService<IAIToolAccessEvaluator>();
        var anonymousTools = services.GetService<IOptions<AnonymousAIToolOptions>>()?.Value;
        var items = new List<object>();

        if (string.IsNullOrWhiteSpace(kind) || kind == "tool")
        {
            var definitions = services.GetService<IOptions<AIToolDefinitionOptions>>()?.Value;

            foreach (var (name, entry) in (definitions?.GetSelectableTools() ?? new Dictionary<string, AIToolDefinitionEntry>())
                .OrderBy(tool => tool.Value.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(tool => tool.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(category) && !string.Equals(entry.Category, category.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Matches(search, name, entry.Title, entry.Description) ||
                    evaluator is null || !await evaluator.IsAuthorizedAsync(caller, name))
                {
                    continue;
                }

                items.Add(new
                {
                    kind = "tool",
                    name,
                    title = entry.Title,
                    description = entry.Description,
                    category = entry.Category,
                    requiresApproval = entry.RequiresApproval,
                    safeForVisitors = anonymousTools?.IsSafe(name) == true,
                });
            }
        }

        if ((string.IsNullOrWhiteSpace(kind) || kind == "toolInstance") && string.IsNullOrWhiteSpace(category))
        {
            var instances = services.GetService<INamedCatalog<AIToolInstance>>();

            foreach (var instance in (instances is null ? Array.Empty<AIToolInstance>() : await instances.GetAllAsync(cancellationToken))
                .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase))
            {
                var functionName = instance.GetFunctionName();

                if (!Matches(search, instance.Name, functionName, instance.Description) ||
                    evaluator is null || !await evaluator.IsAuthorizedAsync(caller, functionName))
                {
                    continue;
                }

                items.Add(new
                {
                    kind = "toolInstance",
                    name = instance.Name,
                    functionName,
                    source = instance.Source,
                    description = instance.Description,
                    safeForVisitors = false,
                });
            }
        }

        if ((string.IsNullOrWhiteSpace(kind) || kind == "agent") && string.IsNullOrWhiteSpace(category))
        {
            var profileManager = services.GetRequiredService<IAIProfileManager>();

            foreach (var agent in (await profileManager.GetAsync(AIProfileType.Agent, cancellationToken))
                .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!Matches(search, agent.Name, agent.DisplayText, agent.Description))
                {
                    continue;
                }

                var capabilities = AIProfileCapabilities.Read(agent);

                items.Add(new
                {
                    kind = "agent",
                    id = agent.ItemId,
                    name = agent.Name,
                    title = agent.DisplayText,
                    description = agent.Description,
                    alwaysAvailable = capabilities.IsAlwaysAvailableAgent,
                    runsTools = capabilities.AllowsToolInvocation && capabilities.HasAnyTool,
                });
            }
        }

        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return AgentToolResults.Json(new
        {
            items = pageItems,
            total = items.Count,
            page,
            pageSize,
            truncated = (page * pageSize) < items.Count,
        });
    }

    private static bool Matches(string search, params string[] values)
        => string.IsNullOrWhiteSpace(search) ||
            values.Any(value => value?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true);
}
