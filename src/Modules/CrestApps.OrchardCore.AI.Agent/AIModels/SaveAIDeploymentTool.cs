using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Connections;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Creates an AI deployment or updates one, through the deployment manager the Deployments page uses and with the
/// checks of its editor: a unique technical name that never changes, a model name, a connection of the deployment's
/// provider, and capabilities the site knows. A key is never taken: a deployment that holds its own connection gets
/// its key from a person on the admin page. Deployments from configuration and the deployments the running assistant
/// uses cannot be changed.
/// </summary>
public sealed class SaveAIDeploymentTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIDeployment";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "deploymentId": {
          "type": "string",
          "description": "The ID of the deployment to change, from listAIDeployments. Leave it out to create a deployment."
        },
        "provider": {
          "type": "string",
          "description": "The provider of a new deployment, as listAIDeployments lists it with includeOptions, for example OpenAI. Ignored on an update."
        },
        "name": {
          "type": "string",
          "description": "The technical name of a new deployment, which profiles and settings refer to. It cannot change after creation, so it is ignored on an update."
        },
        "model": {
          "type": "string",
          "description": "The model name the provider knows, for example gpt-4o-mini. Required for a new deployment."
        },
        "connection": {
          "type": "string",
          "description": "The provider connection to use, by its technical name or ID. Required for a new deployment unless its provider has exactly one connection. Not used by providers whose deployments hold their own connection."
        },
        "capabilities": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The full list of what the model can do, for example textGeneration, toolCalling, streaming, imageInput or realtime. It replaces the stored list. Left out on a new deployment, the capabilities marked as on by default are used."
        },
        "endpoint": {
          "type": "string",
          "description": "For providers whose deployments hold their own connection (speech providers): the address of the provider's API, an absolute https address."
        },
        "authenticationType": {
          "type": "string",
          "enum": ["Default", "ManagedIdentity", "ApiKey"],
          "description": "For an Azure Speech deployment: how it signs in. ApiKey needs a key, which a person enters on the admin page."
        },
        "identityId": {
          "type": "string",
          "description": "For an Azure Speech deployment: the client ID of the managed identity it signs in with. An empty text removes it."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a deployment under a provider or updates one by id: name, model, connection, capabilities and, for speech providers, the contained endpoint. It decides which provider receives the site's AI traffic. Keys are never taken or shown: a deployment that needs a key is finished by a person on the admin page the result links to. Deployments from configuration, and the deployments the running assistant uses, cannot be changed.";

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

        var manager = services.GetRequiredService<INamedSourceCatalogManager<AIDeployment>>();
        var aiOptions = services.GetRequiredService<IOptions<AIOptions>>().Value;
        arguments.TryGetFirstString("deploymentId", out var deploymentId);

        AIDeployment deployment;
        var isNew = string.IsNullOrWhiteSpace(deploymentId);

        if (!isNew)
        {
            deployment = await manager.FindByIdAsync(deploymentId.Trim(), cancellationToken);

            if (deployment is null)
            {
                return $"Nothing was saved: no deployment has the ID '{deploymentId}'. Use listAIDeployments to find it.";
            }
        }
        else
        {
            deployment = null;
        }

        if (AIModelToolSupport.FindSecretArgument(arguments) is { } secretArgument)
        {
            var path = deployment is null ? null : AIModelToolSupport.GetDeploymentEditPath(services, deployment.ItemId);

            return $"Nothing was saved: '{secretArgument}' looks like a key or other secret, and keys are never taken or shown in a chat. Save the deployment without it, then a person types the key on the deployment's admin page{(path is null ? "." : $": {path}")}";
        }

        arguments.TryGetFirstString("model", out var model);
        model = Normalize(model);

        if (isNew)
        {
            arguments.TryGetFirstString("provider", out var provider);
            arguments.TryGetFirstString("name", out var name);
            name = Normalize(name);

            var clientName = string.IsNullOrWhiteSpace(provider)
                ? null
                : aiOptions.Deployments.Keys.FirstOrDefault(key => string.Equals(key, provider.Trim(), StringComparison.OrdinalIgnoreCase));

            if (clientName is null)
            {
                return $"Nothing was saved: give the provider of the new deployment, one of {string.Join(", ", aiOptions.Deployments.Keys.Order(StringComparer.OrdinalIgnoreCase))}.";
            }

            if (name is null)
            {
                return "Nothing was saved: the technical name of the new deployment is required.";
            }

            if (model is null)
            {
                return "Nothing was saved: the model name of the new deployment is required, for example gpt-4o-mini.";
            }

            if (await manager.FindByNameAsync(name, cancellationToken) is not null)
            {
                return $"Nothing was saved: a deployment is already named '{name}'. Update it by its ID instead, or pick another name.";
            }

            deployment = await manager.NewAsync(clientName, cancellationToken: cancellationToken);

            if (deployment is null)
            {
                return $"Nothing was saved: the deployment manager could not create a {clientName} deployment.";
            }

            deployment.Name = name;
        }
        else
        {
            if (deployment.IsReadOnly)
            {
                return $"Nothing was saved: the deployment '{deployment.Name}' is defined in configuration (appsettings) and cannot be changed here. Change it in the site's configuration.";
            }

            if ((await AIConfigurationGuard.GetProtectedModelsAsync(services, cancellationToken)).IsProtected(deployment))
            {
                return AIModelToolSupport.RunningAssistantRefusal($"the deployment '{deployment.Name}'", AIModelToolSupport.GetDeploymentEditPath(services, deployment.ItemId));
            }
        }

        if (model is not null)
        {
            deployment.ModelName = model;
        }

        var contained = aiOptions.Deployments.TryGetValue(deployment.ClientName ?? string.Empty, out var providerEntry) && providerEntry.UseContainedConnection;

        var refusal = contained
            ? ApplyContainedConnection(arguments, deployment)
            : await ApplyConnectionAsync(arguments, services, deployment, isNew, cancellationToken);

        refusal ??= ApplyCapabilities(arguments, services, deployment, isNew);

        if (refusal is not null)
        {
            return $"Nothing was saved: {refusal}";
        }

        var validation = await manager.ValidateAsync(deployment, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}";
        }

        if (isNew)
        {
            await manager.CreateAsync(deployment, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(deployment, cancellationToken: cancellationToken);
        }

        var fields = contained ? AIModelToolSupport.ReadContainedConnectionFields(deployment) : null;
        var editPath = AIModelToolSupport.GetDeploymentEditPath(services, deployment.ItemId);

        return AgentToolResults.Json(new
        {
            created = isNew,
            updated = !isNew,
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
            note = contained && !fields.HasApiKey && !string.Equals(fields.AuthenticationType, "Default", StringComparison.Ordinal) && !string.Equals(fields.AuthenticationType, "ManagedIdentity", StringComparison.Ordinal)
                ? "No key is stored, so the deployment cannot reach its provider yet. Keys are never taken in a chat: a person types it on the admin page."
                : null,
            editPath,
        });
    }

    private static async Task<string> ApplyConnectionAsync(AIFunctionArguments arguments, IServiceProvider services, AIDeployment deployment, bool isNew, CancellationToken cancellationToken)
    {
        if (arguments.ContainsKey("endpoint") || arguments.ContainsKey("authenticationType") || arguments.ContainsKey("identityId"))
        {
            return $"{deployment.ClientName} deployments talk through a provider connection, so endpoint, authenticationType and identityId belong on the connection, which is changed on the AI provider connections admin page.";
        }

        arguments.TryGetFirstString("connection", out var connectionName);
        connectionName = Normalize(connectionName);

        if (connectionName is null && !isNew)
        {
            return null;
        }

        var store = services.GetRequiredService<IAIProviderConnectionStore>();
        var connections = await store.GetAsync(deployment.ClientName, cancellationToken);

        if (connections.Count == 0)
        {
            return $"there is no provider connection for {deployment.ClientName} yet. Create one first on the AI provider connections admin page.";
        }

        AIProviderConnection connection;

        if (connectionName is null)
        {
            if (connections.Count > 1)
            {
                return $"give the connection to use, one of {string.Join(", ", connections.Select(item => item.Name).Order(StringComparer.OrdinalIgnoreCase))}.";
            }

            connection = connections.First();
        }
        else
        {
            connection = await store.FindByConnectionNameAsync(deployment.ClientName, connectionName);

            if (connection is null)
            {
                return $"{deployment.ClientName} has no connection named '{connectionName}'. Use one of {string.Join(", ", connections.Select(item => item.Name).Order(StringComparer.OrdinalIgnoreCase))}.";
            }
        }

        deployment.ConnectionName = string.IsNullOrWhiteSpace(connection.Name) ? connection.ItemId : connection.Name;

        return null;
    }

    private static string ApplyContainedConnection(AIFunctionArguments arguments, AIDeployment deployment)
    {
        if (arguments.TryGetFirstString("connection", out var connection) && !string.IsNullOrWhiteSpace(connection))
        {
            return $"{deployment.ClientName} deployments hold their own connection; leave connection out and give endpoint instead.";
        }

        // Contained-connection deployments keep their connection fields in their properties, as their editors do.
        deployment.ConnectionName = null;
        deployment.Properties ??= new Dictionary<string, object>();

        if (arguments.TryGetFirstString("endpoint", allowEmptyString: true, out var endpoint) && endpoint is not null)
        {
            endpoint = Normalize(endpoint);

            if (endpoint is null)
            {
                deployment.Properties.Remove("Endpoint");
            }
            else if (!AIModelToolSupport.IsWebAddress(endpoint))
            {
                return $"'{endpoint}' is not an absolute web address.";
            }
            else
            {
                deployment.Properties["Endpoint"] = endpoint;
            }
        }

        if (arguments.TryGetFirstString("authenticationType", out var authenticationType) && !string.IsNullOrWhiteSpace(authenticationType))
        {
            var known = AIModelToolSupport.AuthenticationTypes.FirstOrDefault(name => string.Equals(name, authenticationType.Trim(), StringComparison.OrdinalIgnoreCase));

            if (known is null)
            {
                return "authenticationType must be Default, ManagedIdentity or ApiKey.";
            }

            deployment.Properties["AuthenticationType"] = known;
        }

        if (arguments.TryGetFirstString("identityId", allowEmptyString: true, out var identityId) && identityId is not null)
        {
            deployment.Properties["IdentityId"] = Normalize(identityId);
        }

        return null;
    }

    private static string ApplyCapabilities(AIFunctionArguments arguments, IServiceProvider services, AIDeployment deployment, bool isNew)
    {
        if (!AgentToolArguments.TryReadStrings(arguments, "capabilities", out var capabilities))
        {
            return "capabilities must be a list of capability names.";
        }

        var registered = services.GetService<IAIDeploymentCapabilityService>()?.GetRegisteredFeatures() ?? [];

        if (capabilities is null)
        {
            if (!isNew)
            {
                return null;
            }

            capabilities = registered.Where(feature => feature.EnabledByDefault).Select(feature => feature.Name).ToList();
        }

        if (registered.Count > 0)
        {
            var unknown = capabilities
                .Where(name => !registered.Any(feature => string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (unknown.Count > 0)
            {
                return $"the site knows no capability named {string.Join(", ", unknown.Select(name => $"'{name}'"))}. Use {string.Join(", ", registered.Select(feature => feature.Name))}.";
            }

            capabilities = capabilities
                .Select(name => registered.First(feature => string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase)).Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (capabilities.Count == 0)
        {
            return "at least one capability is required, for example textGeneration.";
        }

        var metadata = deployment.GetOrCreate<AIDeploymentMetadata>();
        metadata.Features = [.. capabilities];
        deployment.Put(metadata);

        return null;
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
