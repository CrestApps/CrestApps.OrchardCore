using System.Text.Json;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Connections;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Lists AI provider connections, as the Provider Connections page does, with the deployments that use each. A stored
/// key is only reported as present; it is never read out.
/// </summary>
public sealed class ListAIProviderConnectionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIProviderConnections";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Only connections whose technical name or title contains this text."
        },
        "provider": {
          "type": "string",
          "description": "Only connections of this provider, for example OpenAI or Azure."
        },
        "includeProviders": {
          "type": "boolean",
          "description": "Also list the providers a connection can be created for. Defaults to false."
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
          "description": "How many connections per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists provider connections with provider, name, title, endpoint, authentication type, whether a key is stored, whether the connection comes from configuration (read-only) and which deployments use it. With includeProviders it lists the providers a connection can be created for. Keys are never shown.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageProviderConnections))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("provider", out var provider);
        var includeProviders = arguments.GetFirstValueOrDefault("includeProviders", false);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        IEnumerable<AIProviderConnection> connections = await services.GetRequiredService<IAIProviderConnectionStore>().GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            connections = connections.Where(connection =>
                (connection.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (connection.DisplayText?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            connections = connections.Where(connection => string.Equals(connection.Source, provider.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var filtered = connections
            .OrderBy(connection => connection.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var deployments = (await services.GetRequiredService<INamedSourceCatalogManager<AIDeployment>>().GetAllAsync(cancellationToken)).ToList();
        var aiOptions = services.GetRequiredService<IOptions<AIOptions>>().Value;

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(connection =>
            {
                var fields = AIModelToolSupport.ReadConnectionFields(connection);

                return new
                {
                    id = connection.ItemId,
                    name = connection.Name,
                    title = connection.GetDisplayName(),
                    provider = connection.Source,
                    endpoint = fields.Endpoint,
                    authenticationType = fields.AuthenticationType,
                    identityId = fields.IdentityId,
                    hasApiKey = fields.HasApiKey,
                    fromConfiguration = connection.IsReadOnly,
                    deployments = deployments
                        .Where(deployment => AIModelToolSupport.UsesConnection(deployment, connection))
                        .OrderBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(deployment => new { id = deployment.ItemId, name = deployment.Name })
                        .ToList(),
                    editPath = connection.IsReadOnly ? null : AIModelToolSupport.GetConnectionEditPath(services, connection.ItemId),
                };
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = page * pageSize < filtered.Count,
            providers = includeProviders
                ? aiOptions.ConnectionSources
                    .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new
                    {
                        name = entry.Key,
                        title = entry.Value.DisplayName?.Value ?? entry.Key,
                        description = entry.Value.Description?.Value,
                        createPath = AIModelToolSupport.GetConnectionCreatePath(services, entry.Key),
                    })
                    .ToList()
                : null,
        });
    }
}
