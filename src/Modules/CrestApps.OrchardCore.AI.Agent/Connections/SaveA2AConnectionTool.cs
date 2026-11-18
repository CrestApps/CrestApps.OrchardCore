using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Connections;

/// <summary>
/// Creates or updates a connection to a remote Agent-to-Agent host through the A2A connection manager, with the
/// checks of its editor. Credentials are never taken: a new connection is anonymous, and an existing one keeps the
/// credentials stored for its authentication, which a person changes on the admin page. The connections the running
/// assistant uses cannot be changed from a chat.
/// </summary>
public sealed class SaveA2AConnectionTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveA2AConnection";

    private static readonly string[] _ownArguments = ["connectionId", "title", "endpoint"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    $$"""
    {
      "type": "object",
      "properties": {
        "connectionId": {
          "type": "string",
          "description": "The ID of the connection to update, from listA2AConnections. Leave it out to create a connection."
        },
        "title": {
          "type": "string",
          "description": "The title of the connection. Required to create one."
        },
        "endpoint": {
          "type": "string",
          "description": "The absolute http or https address of the remote A2A host. Required to create a connection. Profiles that use it send conversation content to that host, so confirm it with the user."
        },
    {{RemoteConnectionAuth.SchemaProperties}}
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a connection to a remote A2A host: title, HTTP or HTTPS endpoint and authentication. Credentials are never taken or shown: a new connection is anonymous, and a key or secret is entered by a person on the admin page the result links to. Profiles that use it send conversation content to that host.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIModulePermissions.ManageA2AConnections))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ICatalogManager<A2AConnection>>();
        var isNew = !arguments.TryGetFirstString("connectionId", out var connectionId) || string.IsNullOrWhiteSpace(connectionId);
        A2AConnection connection = null;

        if (!isNew)
        {
            connection = await manager.FindByIdAsync(connectionId.Trim(), cancellationToken);

            if (connection is null)
            {
                return $"Nothing was saved: no A2A connection has the ID '{connectionId.Trim()}'. Use listA2AConnections to find it.";
            }
        }

        var adminPath = isNew
            ? ConnectionToolSupport.GetA2AConnectionCreatePath(services)
            : ConnectionToolSupport.GetA2AConnectionEditPath(services, connection.ItemId);

        var credentialArgument = RemoteConnectionAuth.FindCredentialArgument(arguments);

        if (credentialArgument is not null)
        {
            return ConnectionToolSupport.CredentialRefusal(credentialArgument, adminPath);
        }

        var unknown = arguments.Keys.FirstOrDefault(name => !_ownArguments.Contains(name, StringComparer.Ordinal) && !RemoteConnectionAuth.ArgumentNames.Contains(name, StringComparer.Ordinal));

        if (unknown is not null)
        {
            return $"Nothing was saved: '{unknown}' is not an argument of this tool.";
        }

        if (!isNew)
        {
            var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);

            if (protectedConfiguration.IsProtectedA2AConnection(connection.ItemId))
            {
                return ConnectionToolSupport.RunningAssistantRefusal($"the A2A connection '{connection.DisplayText}'", adminPath);
            }
        }

        var stored = connection?.TryGet<A2AConnectionMetadata>(out var storedMetadata) == true ? storedMetadata : new A2AConnectionMetadata();
        var changes = new List<string>();

        var hasTitle = arguments.TryGetFirstString("title", out var title);
        title = title?.Trim();

        if ((isNew || hasTitle) && string.IsNullOrWhiteSpace(title))
        {
            return "Nothing was saved: The Title is required.";
        }

        var hasEndpoint = arguments.TryGetFirstString("endpoint", out var endpoint);
        endpoint = endpoint?.Trim();

        if (isNew || hasEndpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return "Nothing was saved: The Endpoint is required.";
            }

            if (!ConnectionToolSupport.IsWebAddress(endpoint))
            {
                return "Nothing was saved: The Endpoint must be a valid HTTP or HTTPS URL.";
            }

            if (!isNew && RemoteConnectionAuth.MovesCredentials(stored, connection.Endpoint, endpoint))
            {
                return $"Nothing was saved: the connection stores credentials, and moving it to another host would send them there. Change the address on the admin page, where the credentials are entered again: {adminPath}";
            }
        }

        var fields = RemoteAuthFields.From(stored);
        var auth = RemoteConnectionAuth.Apply(arguments, fields, isNew);

        if (auth.Error is not null)
        {
            return $"Nothing was saved: {auth.Error}";
        }

        if (auth.CredentialType is not null)
        {
            return ConnectionToolSupport.NeedsCredentialRefusal(auth.CredentialType, adminPath, isNew);
        }

        if (isNew)
        {
            connection = await manager.NewAsync(new JsonObject(), cancellationToken);
        }
        else
        {
            if (hasTitle && !string.Equals(connection.DisplayText, title, StringComparison.Ordinal))
            {
                changes.Add("title");
            }

            if (hasEndpoint && !string.Equals(connection.Endpoint, endpoint, StringComparison.Ordinal))
            {
                changes.Add("endpoint");
            }

            changes.AddRange(auth.Changes);

            if (changes.Count == 0)
            {
                return AgentToolResults.Json(new
                {
                    id = connection.ItemId,
                    title = connection.DisplayText,
                    changed = changes,
                    note = "Nothing was different from the stored connection, so nothing was saved.",
                });
            }
        }

        if (hasTitle || isNew)
        {
            connection.DisplayText = title;
        }

        if (hasEndpoint || isNew)
        {
            connection.Endpoint = endpoint;
        }

        var metadata = new A2AConnectionMetadata();
        fields.WriteTo(metadata);
        connection.Put(metadata);

        var validation = await manager.ValidateAsync(connection, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}";
        }

        if (isNew)
        {
            await manager.CreateAsync(connection, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(connection, new JsonObject(), cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            id = connection.ItemId,
            created = isNew,
            title = connection.DisplayText,
            endpoint = connection.Endpoint,
            changed = isNew ? null : changes,
            authentication = RemoteConnectionAuth.Describe(metadata),
            editPath = ConnectionToolSupport.GetA2AConnectionEditPath(services, connection.ItemId),
            note = isNew
                ? "The connection was created with anonymous access. If the host needs a key or other credential, a person enters it on the editPath page. Tick the connection on a profile's Capabilities tab to use its agents."
                : null,
        });
    }
}
