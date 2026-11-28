using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.ToolInstances;

/// <summary>
/// Lists the AI tool instances, as the Tool Instances admin page does, with their non-secret settings and the profiles
/// that use them. A stored credential is reported only as present or absent.
/// </summary>
public sealed class ListAIToolInstancesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIToolInstances";

    private const int MaxProfileTitles = 20;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Only list tool instances whose name contains this text."
        },
        "source": {
          "type": "string",
          "description": "Only list tool instances of this source, for example http-api-request or sitemap-documentation."
        },
        "includeSources": {
          "type": "boolean",
          "description": "Also list the sources a tool instance can be created from and the settings each one takes."
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
          "description": "How many tool instances a page holds, 20 when left out and at most 50."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists tool instances with source, name, description, function name, owner, non-secret settings and which profiles use them. With includeSources it also lists the available sources and the settings each one takes. Stored credentials are reported only as present or absent.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIToolInstances))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("source", out var source);
        var includeSources = arguments.GetFirstValueOrDefault("includeSources", false);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var manager = services.GetRequiredService<ISourceCatalogManager<AIToolInstance>>();
        var result = await manager.PageAsync(page, pageSize, new QueryContext
        {
            Sorted = true,
            Name = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
        }, cancellationToken);

        var usedBy = await GetProfilesByInstanceAsync(services.GetRequiredService<IAIProfileManager>(), cancellationToken);
        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<object>();

        foreach (var instance in result.Entries)
        {
            var (settings, credentials) = ToolInstanceSettings.Read(instance);
            usedBy.TryGetValue(instance.Name ?? string.Empty, out var profiles);

            items.Add(new
            {
                id = instance.ItemId,
                name = instance.Name,
                functionName = instance.GetFunctionName(),
                source = instance.Source,
                description = instance.Description,
                author = instance.Author,
                created = await AgentToolResults.ToLocalAsync(localClock, instance.CreatedUtc),
                modified = await AgentToolResults.ToLocalAsync(localClock, instance.ModifiedUtc),
                settings,
                credentials = credentials.Count > 0 ? credentials : null,
                usedByProfileCount = profiles?.Count ?? 0,
                usedByProfiles = profiles?.Take(MaxProfileTitles).ToArray() ?? [],
                editPath = AIProfileToolSupport.GetToolInstanceEditPath(services, instance.ItemId),
            });
        }

        object sources = null;

        if (includeSources)
        {
            var options = services.GetService<IOptions<AIOptions>>()?.Value;

            sources = (options?.ToolInstanceSources ?? new Dictionary<string, AIToolInstanceSourceEntry>())
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry =>
                {
                    var settingNames = ToolInstanceSettings.GetSettingNames(entry.Key);

                    return new
                    {
                        source = entry.Key,
                        title = entry.Value.DisplayName?.Value,
                        description = entry.Value.Description?.Value,
                        category = entry.Value.Category?.Value,
                        settings = settingNames,
                        note = settingNames.Count == 0 ? "The settings of this source are set on its admin page." : null,
                    };
                })
                .ToList();
        }

        return AgentToolResults.Json(new
        {
            items,
            total = result.Count,
            page,
            pageSize,
            truncated = (page * pageSize) < result.Count,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            sources,
        });
    }

    private static async Task<Dictionary<string, List<string>>> GetProfilesByInstanceAsync(IAIProfileManager profileManager, CancellationToken cancellationToken)
    {
        var usedBy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in await profileManager.GetAllAsync(cancellationToken))
        {
            foreach (var instanceName in AIProfileCapabilities.Read(profile).ToolInstanceNames)
            {
                if (!usedBy.TryGetValue(instanceName, out var titles))
                {
                    usedBy[instanceName] = titles = [];
                }

                titles.Add(AIConfigurationGuard.GetTitle(profile));
            }
        }

        return usedBy;
    }
}
