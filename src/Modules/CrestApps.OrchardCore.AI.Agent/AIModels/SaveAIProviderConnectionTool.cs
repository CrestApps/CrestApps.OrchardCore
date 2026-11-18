using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Creates an AI provider connection or updates one, through the connection manager the Provider Connections page and
/// the recipe step use, and reloads the site as the page does. A key is never taken: a connection that needs one is
/// finished by a person on the admin page. Connections from configuration and the connection the running assistant
/// talks through cannot be changed.
/// </summary>
public sealed class SaveAIProviderConnectionTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIProviderConnection";

    /// <summary>
    /// What the result says about the reload.
    /// </summary>
    internal const string ReloadNote = "The site reloads once this chat message is done, so every AI feature picks the change up. The reload can end this chat; if it does, open the chat again.";

    private const string ApiKeyField = "ApiKey";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "connectionId": {
          "type": "string",
          "description": "The ID of the connection to change, from listAIProviderConnections. Leave it out to create a connection."
        },
        "provider": {
          "type": "string",
          "description": "The provider of a new connection, as listAIProviderConnections lists it with includeProviders, for example OpenAI or Azure. Ignored on an update."
        },
        "name": {
          "type": "string",
          "description": "The technical name of a new connection, which deployments refer to. It cannot change after creation, so it is ignored on an update."
        },
        "title": {
          "type": "string",
          "description": "The title people see."
        },
        "endpoint": {
          "type": "string",
          "description": "The address of the provider's API, an absolute https address."
        },
        "authenticationType": {
          "type": "string",
          "enum": ["Default", "ManagedIdentity", "ApiKey"],
          "description": "How an Azure connection signs in. ApiKey needs a key, which a person enters on the admin page."
        },
        "identityId": {
          "type": "string",
          "description": "The client ID of the managed identity an Azure connection signs in with. An empty text removes it."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a provider connection or updates one by id. Keys are never taken or shown: a connection that needs a key is finished by a person on the admin page the result links to. Saving reloads the site, which can end the current chat, so do it last. Connections from configuration, and the connection the running assistant uses, cannot be changed.";

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

        var manager = services.GetRequiredService<INamedSourceCatalogManager<AIProviderConnection>>();
        arguments.TryGetFirstString("connectionId", out var connectionId);

        AIProviderConnection existing = null;

        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            existing = await manager.FindByIdAsync(connectionId.Trim(), cancellationToken);

            if (existing is null)
            {
                return $"Nothing was saved: no provider connection has the ID '{connectionId}'. Use listAIProviderConnections to find it.";
            }
        }

        if (AIModelToolSupport.FindSecretArgument(arguments) is { } secretArgument)
        {
            var path = existing is null
                ? AIModelToolSupport.GetConnectionsPath(services)
                : AIModelToolSupport.GetConnectionEditPath(services, existing.ItemId);

            return $"Nothing was saved: '{secretArgument}' looks like a key or other secret, and keys are never taken or shown in a chat. Save the connection without it, then a person types the key on the admin page{(path is null ? "." : $": {path}")}";
        }

        arguments.TryGetFirstString("endpoint", out var endpoint);
        endpoint = Normalize(endpoint);

        if (endpoint is not null && !AIModelToolSupport.IsWebAddress(endpoint))
        {
            return $"Nothing was saved: '{endpoint}' is not an absolute web address. Give the provider's API address, for example https://contoso.openai.azure.com/.";
        }

        arguments.TryGetFirstString("authenticationType", out var authenticationType);

        if (!string.IsNullOrWhiteSpace(authenticationType))
        {
            authenticationType = AIModelToolSupport.AuthenticationTypes.FirstOrDefault(name => string.Equals(name, authenticationType.Trim(), StringComparison.OrdinalIgnoreCase));

            if (authenticationType is null)
            {
                return "Nothing was saved: authenticationType must be Default, ManagedIdentity or ApiKey.";
            }
        }
        else
        {
            authenticationType = null;
        }

        var hasIdentity = arguments.TryGetFirstString("identityId", allowEmptyString: true, out var identityId) && identityId is not null;
        arguments.TryGetFirstString("title", out var title);

        return existing is null
            ? await CreateAsync(arguments, services, manager, Normalize(title), endpoint, authenticationType, Normalize(identityId), cancellationToken)
            : await UpdateAsync(services, manager, existing, Normalize(title), endpoint, authenticationType, hasIdentity, Normalize(identityId), cancellationToken);
    }

    private static async Task<string> CreateAsync(
        AIFunctionArguments arguments,
        IServiceProvider services,
        INamedSourceCatalogManager<AIProviderConnection> manager,
        string title,
        string endpoint,
        string authenticationType,
        string identityId,
        CancellationToken cancellationToken)
    {
        arguments.TryGetFirstString("provider", out var provider);
        arguments.TryGetFirstString("name", out var name);
        name = Normalize(name);

        var sources = services.GetRequiredService<IOptions<AIOptions>>().Value.ConnectionSources;
        var source = string.IsNullOrWhiteSpace(provider)
            ? null
            : sources.Keys.FirstOrDefault(key => string.Equals(key, provider.Trim(), StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return $"Nothing was saved: give the provider of the new connection, one of {string.Join(", ", sources.Keys.Order(StringComparer.OrdinalIgnoreCase))}.";
        }

        if (name is null)
        {
            return "Nothing was saved: the technical name of the new connection is required.";
        }

        var data = new JsonObject
        {
            [nameof(AIProviderConnection.Name)] = name,
            [nameof(AIProviderConnection.DisplayText)] = title ?? name,
        };

        AddConnectionFields(data, endpoint, authenticationType, identityId);

        var connection = await manager.NewAsync(source, data, cancellationToken);

        if (connection is null)
        {
            return $"Nothing was saved: the connection manager could not create a {source} connection.";
        }

        var validation = await manager.ValidateAsync(connection, cancellationToken);

        if (!validation.Succeeded)
        {
            var errors = validation.Errors.ToList();
            var createPath = AIModelToolSupport.GetConnectionCreatePath(services, source);

            // A key is never taken here, so a connection that cannot exist without one is created by a person.
            if (errors.All(error => error.MemberNames.Contains(ApiKeyField, StringComparer.OrdinalIgnoreCase)))
            {
                return $"Nothing was saved: a {source} connection{(authenticationType is null ? string.Empty : $" with {authenticationType} authentication")} needs an API key, and keys are never taken in a chat. A person can create it with its key on the admin page{(createPath is null ? "." : $": {createPath}")}";
            }

            return $"Nothing was saved: {string.Join(" ", errors.Select(error => error.ErrorMessage))}";
        }

        await manager.CreateAsync(connection, cancellationToken);
        services.GetRequiredService<IShellReleaseManager>().RequestRelease();

        return Result(services, connection, created: true);
    }

    private static async Task<string> UpdateAsync(
        IServiceProvider services,
        INamedSourceCatalogManager<AIProviderConnection> manager,
        AIProviderConnection connection,
        string title,
        string endpoint,
        string authenticationType,
        bool hasIdentity,
        string identityId,
        CancellationToken cancellationToken)
    {
        var editPath = AIModelToolSupport.GetConnectionEditPath(services, connection.ItemId);

        if (connection.IsReadOnly)
        {
            return $"Nothing was saved: the connection '{connection.GetDisplayName()}' is defined in configuration (appsettings) and cannot be changed here. Change it in the site's configuration.";
        }

        if ((await AIConfigurationGuard.GetProtectedModelsAsync(services, cancellationToken)).IsProtected(connection))
        {
            return AIModelToolSupport.RunningAssistantRefusal($"the connection '{connection.GetDisplayName()}'", editPath);
        }

        var stored = AIModelToolSupport.ReadConnectionFields(connection);
        var nextAuthenticationType = authenticationType ?? stored.AuthenticationType;

        if (string.Equals(nextAuthenticationType, "ApiKey", StringComparison.Ordinal) && !stored.HasApiKey)
        {
            return $"Nothing was saved: ApiKey authentication needs a key, and the connection '{connection.GetDisplayName()}' has none. Keys are never taken in a chat; a person can choose ApiKey and type the key on the admin page{(editPath is null ? "." : $": {editPath}")}";
        }

        var data = new JsonObject();

        if (title is not null)
        {
            data[nameof(AIProviderConnection.DisplayText)] = title;
        }

        // The provider handlers reset what a payload leaves out, so every connection field goes with any change.
        if (endpoint is not null || authenticationType is not null || hasIdentity)
        {
            AddConnectionFields(
                data,
                endpoint ?? stored.Endpoint,
                nextAuthenticationType,
                hasIdentity ? identityId : stored.IdentityId);
        }

        if (data.Count == 0)
        {
            return "Nothing was saved: give at least one of title, endpoint, authenticationType or identityId to change.";
        }

        await manager.UpdateAsync(connection, data, cancellationToken);
        services.GetRequiredService<IShellReleaseManager>().RequestRelease();

        var validation = await manager.ValidateAsync(connection, cancellationToken);

        return validation.Succeeded
            ? Result(services, connection, created: false)
            : Result(services, connection, created: false, problems: validation.Errors.Select(error => error.ErrorMessage).ToList());
    }

    private static void AddConnectionFields(JsonObject data, string endpoint, string authenticationType, string identityId)
    {
        if (endpoint is not null)
        {
            data["Endpoint"] = endpoint;
        }

        if (authenticationType is not null)
        {
            data["AuthenticationType"] = authenticationType;
        }

        if (identityId is not null)
        {
            data["IdentityId"] = identityId;
        }
    }

    private static string Result(IServiceProvider services, AIProviderConnection connection, bool created, List<string> problems = null)
    {
        var fields = AIModelToolSupport.ReadConnectionFields(connection);
        var editPath = AIModelToolSupport.GetConnectionEditPath(services, connection.ItemId);
        var needsKey = !fields.HasApiKey && !string.Equals(fields.AuthenticationType, "Default", StringComparison.Ordinal) &&
            !string.Equals(fields.AuthenticationType, "ManagedIdentity", StringComparison.Ordinal);

        return AgentToolResults.Json(new
        {
            created,
            updated = !created,
            id = connection.ItemId,
            name = connection.Name,
            title = connection.GetDisplayName(),
            provider = connection.Source,
            endpoint = fields.Endpoint,
            authenticationType = fields.AuthenticationType,
            identityId = fields.IdentityId,
            hasApiKey = fields.HasApiKey,
            siteReloads = true,
            note = needsKey
                ? $"{ReloadNote} No key is stored; if the provider needs one, a person types it on the admin page."
                : ReloadNote,
            problems,
            editPath,
        });
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
