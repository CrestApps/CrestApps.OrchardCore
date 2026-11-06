using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Settings;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Connections;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Handles catalog lifecycle events for <see cref="AIDeployment"/> entries, including initialization, validation, and population from JSON data.
/// </summary>
public sealed class AIDeploymentHandler : CatalogEntryHandlerBase<AIDeployment>
{
    // Recipes are written by hand, so the pricing section is read without regard to case and accepts numbers as text.
    private static readonly JsonSerializerOptions _pricingSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAIProviderConnectionStore _connectionStore;
    private readonly AIOptions _aiOptions;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor for retrieving the current user.</param>
    /// <param name="connectionsCatalog">The catalog of provider connections used for validation.</param>
    /// <param name="aiOptions">The AI options containing deployment configuration.</param>
    /// <param name="clock">The clock service for obtaining the current UTC time.</param>
    /// <param name="stringLocalizer">The string localizer for validation messages.</param>
    public AIDeploymentHandler(
        IHttpContextAccessor httpContextAccessor,
        IAIProviderConnectionStore connectionsCatalog,
        IOptions<AIOptions> aiOptions,
        IClock clock,
        IStringLocalizer<AIDeploymentHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _connectionStore = connectionsCatalog;
        _aiOptions = aiOptions.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIDeployment> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIDeployment> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return PopulateAsync(context.Model, context.Data);
    }

    public override async Task ValidatingAsync(ValidatingContext<AIDeployment> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Deployment Name is required."], [nameof(AIDeployment.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.ModelName))
        {
            context.Result.Fail(new ValidationResult(S["Model name is required."], [nameof(AIDeployment.ModelName)]));
        }

        // Validated on capabilities rather than the legacy purpose. PopulateAsync has already projected any
        // legacy purpose onto features, so a payload that only carries a purpose still satisfies this, while
        // a realtime-only deployment -- which deliberately declares no text generation -- now passes where the
        // purpose check used to be the only thing describing it.
        if (!context.Model.TryGet<AIDeploymentMetadata>(out var metadata) || metadata.Features is not { Length: > 0 })
        {
            context.Result.Fail(new ValidationResult(S["At least one model capability is required."], [nameof(AIDeployment.Properties)]));
        }

        var requiresConnection = !HasContainedConnection(context.Model.ClientName);
        var hasConnectionName = true;

        if (requiresConnection && string.IsNullOrWhiteSpace(context.Model.ConnectionName))
        {
            hasConnectionName = false;
            context.Result.Fail(new ValidationResult(S["Connection name is required."], [nameof(AIDeployment.ConnectionName)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.ClientName))
        {
            context.Result.Fail(new ValidationResult(S["Provider is required."], [nameof(AIDeployment.ClientName)]));
        }
        else
        {
            if (hasConnectionName)
            {
                var connections = await _connectionStore.GetAsync(context.Model.ClientName, cancellationToken);

                if (connections.Count == 0)
                {
                    context.Result.Fail(new ValidationResult(S["There are no configured connection for the provider: {0}", context.Model.ClientName], [nameof(AIDeployment.ClientName)]));
                }
                else if (await _connectionStore.FindByConnectionNameAsync(context.Model.ClientName, context.Model.ConnectionName) is null)
                {
                    context.Result.Fail(new ValidationResult(S["Invalid connection name provided."], [nameof(AIDeployment.ConnectionName)]));
                }
            }
        }

        return;
    }

    public override Task InitializedAsync(InitializedContext<AIDeployment> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIDeployment deployment, JsonNode data)
    {
        // An update that carries no payload, such as one made in code, changes nothing here.
        if (data is null)
        {
            return Task.CompletedTask;
        }

        var name = data[nameof(AIDeployment.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            deployment.Name = name;
        }

        var modelName = data[nameof(AIDeployment.ModelName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(modelName))
        {
            deployment.ModelName = modelName;
        }
        else if (!string.IsNullOrWhiteSpace(deployment.Name) && string.IsNullOrWhiteSpace(deployment.ModelName))
        {
            deployment.ModelName = deployment.Name;
        }

        var clientName = data[nameof(AIDeployment.ClientName)]?.GetValue<string>()?.Trim()
            ?? data["ProviderName"]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(clientName))
        {
            deployment.ClientName = clientName;
        }

        var connectionName = data[nameof(AIDeployment.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            deployment.ConnectionName = connectionName;
        }

        PopulateContainedConnectionAliases(deployment, data);

        TryGetLegacyPurposes(data, out var legacyPurposes);

        var properties = data[nameof(AIDeployment.Properties)]?.AsObject();

        if (properties != null)
        {
            deployment.Properties ??= new Dictionary<string, object>();

            var currentJson = JsonSerializer.SerializeToNode(deployment.Properties)?.AsObject() ?? [];
            // A list in the payload replaces the stored one. Concatenating them would repeat every capability each
            // time the same deployment is imported again.
            currentJson.Merge(properties, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
            });
            deployment.Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? [];
        }

        PopulatePricing(deployment, data);

        // Runs after the properties merge so the already-declared features are visible to the rule. This is
        // the recipe, configuration, and API half of the read-time normalization; the store deserialization
        // half is AIDeployment.OnDeserialized. It goes through LegacyAIDeploymentCapabilities rather than
        // the framework directly so an imported legacy deployment declares the same capabilities the store
        // migrations give one, tool calling and streaming included.
        LegacyAIDeploymentCapabilities.Normalize(deployment, legacyPurposes);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the deployment's prices from the <c>Pricing</c> section a recipe or API payload carries. Prices given
    /// under <c>Properties.AIDeploymentPricing</c> are read the same way, so they are normalized too. A <c>Pricing</c>
    /// section set to <see langword="null"/> removes the deployment's prices; one that is left out keeps them.
    /// </summary>
    private static void PopulatePricing(AIDeployment deployment, JsonNode data)
    {
        if (data is not JsonObject payload)
        {
            return;
        }

        JsonNode pricingNode;

        if (payload.TryGetPropertyValue("Pricing", out pricingNode))
        {
            if (pricingNode is null)
            {
                deployment.SetPricing(null, []);

                return;
            }
        }
        else if (payload[nameof(AIDeployment.Properties)]?[nameof(AIDeploymentPricing)] is not { } propertiesPricing)
        {
            return;
        }
        else
        {
            pricingNode = propertiesPricing;
        }

        var pricing = pricingNode.Deserialize<AIDeploymentPricing>(_pricingSerializerOptions);

        deployment.SetPricing(pricing?.Currency, pricing?.Prices);
    }

    private static void PopulateContainedConnectionAliases(AIDeployment deployment, JsonNode data)
    {
        CopyStringProperty(data, deployment, "Endpoint");
        CopyStringProperty(data, deployment, "AuthenticationType");
        CopyStringProperty(data, deployment, "ApiKey");
        CopyStringProperty(data, deployment, "IdentityId");
    }

    private static void CopyStringProperty(JsonNode data, AIDeployment deployment, string propertyName)
    {
        var propertyValue = data[propertyName]?.GetValue<string>();

        if (propertyValue is null)
        {
            return;
        }

        deployment.Properties ??= new Dictionary<string, object>();
        deployment.Properties[propertyName] = propertyValue;
    }

    /// <summary>
    /// Reads the legacy purpose names a payload may still carry, under any of the three field names this
    /// field has had. The names themselves are interpreted by
    /// <see cref="AIDeploymentPurposeCompatibility"/>, which ignores any it does not recognize.
    /// </summary>
    private static bool TryGetLegacyPurposes(JsonNode data, out string[] legacyPurposes)
    {
        legacyPurposes = null;

        if (data is null)
        {
            return false;
        }

        var purposeNode = data["Purpose"] ?? data["Capability"] ?? data["Type"];

        if (purposeNode is null)
        {
            return false;
        }

        if (purposeNode is JsonArray array)
        {
            legacyPurposes = [.. array
                .Select(static item => item?.GetValue<string>())
                .Where(static name => !string.IsNullOrWhiteSpace(name))];

            return legacyPurposes.Length > 0;
        }

        var purposeText = purposeNode.GetValue<string>();

        if (string.IsNullOrWhiteSpace(purposeText))
        {
            return false;
        }

        legacyPurposes = [purposeText];

        return true;
    }

    private bool HasContainedConnection(string clientName)
        => !string.IsNullOrWhiteSpace(clientName) &&
            _aiOptions.Deployments.TryGetValue(clientName, out var entry) &&
                entry.UseContainedConnection;
}
