using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.ProfileTemplates;

/// <summary>
/// Lists AI profile templates, both the ones stored on the site and the ones shipped with a module, as the Templates
/// admin page does, with whether a Profile template's scenario can be used on this site.
/// </summary>
public sealed class ListAIProfileTemplatesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIProfileTemplates";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "source": {
          "type": "string",
          "enum": ["Profile", "SystemPrompt"],
          "description": "Only list templates of this source: Profile (a whole profile setup) or SystemPrompt (a block of instructions profiles select)."
        },
        "category": {
          "type": "string",
          "description": "Only list templates of this category, for example 'Speaking style' or 'Guardrail topic'."
        },
        "search": {
          "type": "string",
          "description": "Only list templates whose name, title or description contains this text."
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
          "description": "How many templates a page holds, 20 when left out and at most 50."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists templates with source (Profile or SystemPrompt), category, title, whether they are shipped with a module (read-only) and whether their scenario is available. Filter by source, category (for example speaking styles or guardrail topics) or name.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIProfileTemplates))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("source", out var source);
        arguments.TryGetFirstString("category", out var category);
        arguments.TryGetFirstString("search", out var search);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var templateManager = services.GetRequiredService<IAIProfileTemplateManager>();
        var storedIds = await ProfileTemplateToolSupport.GetStoredIdsAsync(services, cancellationToken);
        var shellFeaturesManager = services.GetService<IShellFeaturesManager>();
        var features = shellFeaturesManager is null ? null : await ProfileScenarioFeatureLookup.CreateAsync(shellFeaturesManager);

        var templates = (await templateManager.GetAllAsync(cancellationToken))
            .Where(template => string.IsNullOrWhiteSpace(source) || string.Equals(template.Source, source.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(template => string.IsNullOrWhiteSpace(category) || string.Equals(template.Category?.Trim(), category.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(template => string.IsNullOrWhiteSpace(search) ||
                template.Name?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true ||
                template.DisplayText?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true ||
                template.Description?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(template => template.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.DisplayText ?? template.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = templates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(template =>
            {
                var isProfile = string.Equals(template.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase);
                var missing = isProfile && features is not null
                    ? features.GetMissingFeatureNames(template.GetOrCreate<ProfileScenarioMetadata>())
                    : new List<string>();

                return new
                {
                    id = template.ItemId,
                    name = template.Name,
                    title = template.DisplayText,
                    description = template.Description,
                    source = template.Source,
                    category = template.Category,
                    isListable = template.IsListable,
                    isShipped = !storedIds.Contains(template.ItemId ?? string.Empty),
                    profileType = isProfile ? template.GetOrCreate<ProfileTemplateMetadata>().ProfileType?.ToString() : null,
                    isAvailable = isProfile ? missing.Count == 0 : (bool?)null,
                    missingFeatures = missing.Count > 0 ? missing : null,
                };
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = templates.Count,
            page,
            pageSize,
            truncated = (page * pageSize) < templates.Count,
        });
    }
}
