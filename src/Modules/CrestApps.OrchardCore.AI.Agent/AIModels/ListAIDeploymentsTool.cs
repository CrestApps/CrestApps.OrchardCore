using System.Text.Json;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Lists AI deployments, as the Deployments page does, with their capabilities, prices, the slots they are the site's
/// default for and the profiles that name them. A key a deployment holds itself is only reported as present.
/// </summary>
public sealed class ListAIDeploymentsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIDeployments";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Only deployments whose technical name or model name contains this text."
        },
        "provider": {
          "type": "string",
          "description": "Only deployments of this provider, for example OpenAI or ElevenLabs."
        },
        "connection": {
          "type": "string",
          "description": "Only deployments that use this provider connection, by its technical name or ID."
        },
        "capability": {
          "type": "string",
          "description": "Only deployments that declare this capability, for example textGeneration, toolCalling, textToSpeech or realtime."
        },
        "includeOptions": {
          "type": "boolean",
          "description": "Also list the providers a deployment can be created for and the capabilities a deployment can declare. Defaults to false."
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
          "description": "How many deployments per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists deployments with provider, connection, model, capabilities, prices, whether a contained key is stored, whether they come from configuration, and which slot defaults and profiles use them. Filter by provider, connection or capability. Keys are never shown.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIDeployments))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("provider", out var provider);
        arguments.TryGetFirstString("connection", out var connection);
        arguments.TryGetFirstString("capability", out var capability);
        var includeOptions = arguments.GetFirstValueOrDefault("includeOptions", false);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        IEnumerable<AIDeployment> deployments = await services.GetRequiredService<INamedSourceCatalogManager<AIDeployment>>().GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            deployments = deployments.Where(deployment =>
                (deployment.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (deployment.ModelName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            deployments = deployments.Where(deployment => string.Equals(deployment.ClientName, provider.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(connection))
        {
            deployments = deployments.Where(deployment => string.Equals(deployment.ConnectionName, connection.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(capability))
        {
            deployments = deployments.Where(deployment => AIModelToolSupport.GetFeatures(deployment).Contains(capability.Trim(), StringComparer.OrdinalIgnoreCase));
        }

        var filtered = deployments
            .OrderBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var aiOptions = services.GetRequiredService<IOptions<AIOptions>>().Value;
        var slotDefaults = AIModelToolSupport.GetSlotDefaults(await services.GetRequiredService<ISiteService>().GetSettingsAsync<DefaultAIDeploymentSettings>()).ToList();
        var profiles = (await services.GetRequiredService<IAIProfileManager>().GetAllAsync(cancellationToken)).ToList();

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(deployment =>
            {
                var contained = aiOptions.Deployments.TryGetValue(deployment.ClientName ?? string.Empty, out var entry) && entry.UseContainedConnection;
                var fields = contained ? AIModelToolSupport.ReadContainedConnectionFields(deployment) : null;
                var pricing = deployment.GetPricing();

                return new
                {
                    id = deployment.ItemId,
                    name = deployment.Name,
                    model = deployment.ModelName,
                    provider = deployment.ClientName,
                    connection = contained ? null : deployment.ConnectionName,
                    holdsOwnConnection = contained,
                    endpoint = fields?.Endpoint,
                    authenticationType = fields?.AuthenticationType,
                    hasApiKey = contained ? fields.HasApiKey : (bool?)null,
                    capabilities = AIModelToolSupport.GetFeatures(deployment),
                    pricing = pricing is null ? null : new
                    {
                        currency = pricing.Currency,
                        prices = (pricing.Prices ?? []).Select(price => new
                        {
                            meter = price.Meter,
                            amount = price.Amount,
                            per = price.Per,
                            countName = price.CountName,
                            aboveInputTokens = price.AboveInputTokens,
                            increment = price.Increment > 0 ? price.Increment : (long?)null,
                        }).ToList(),
                    },
                    fromConfiguration = deployment.IsReadOnly,
                    defaultFor = slotDefaults.Where(slot => AIModelToolSupport.IsNamed(deployment, slot.DeploymentName)).Select(slot => slot.Slot).ToList(),
                    profiles = profiles
                        .Where(profile => AIModelToolSupport.IsNamed(deployment, profile.ChatDeploymentName) || AIModelToolSupport.IsNamed(deployment, profile.UtilityDeploymentName))
                        .Select(profile => string.IsNullOrWhiteSpace(profile.DisplayText) ? profile.Name : profile.DisplayText)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    editPath = deployment.IsReadOnly ? null : AIModelToolSupport.GetDeploymentEditPath(services, deployment.ItemId),
                };
            })
            .ToList();

        var capabilityService = services.GetService<IAIDeploymentCapabilityService>();

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = page * pageSize < filtered.Count,
            providers = includeOptions
                ? aiOptions.Deployments
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new
                    {
                        name = pair.Key,
                        title = pair.Value.DisplayName?.Value ?? pair.Key,
                        holdsOwnConnection = pair.Value.UseContainedConnection,
                        createPath = AIModelToolSupport.GetDeploymentCreatePath(services, pair.Key),
                    })
                    .ToList()
                : null,
            capabilities = includeOptions && capabilityService is not null
                ? capabilityService.GetRegisteredFeatures()
                    .OrderBy(feature => feature.Order)
                    .Select(feature => new { name = feature.Name, title = feature.DisplayName?.Value ?? feature.Name })
                    .ToList()
                : null,
        });
    }
}
