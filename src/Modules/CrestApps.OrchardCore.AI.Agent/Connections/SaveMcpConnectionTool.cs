using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.Connections;

/// <summary>
/// Creates or updates a remote (server-sent events) MCP connection through the MCP connection manager, with the
/// checks of its editor. Credentials are never taken: a new connection is anonymous, and an existing one keeps the
/// credentials stored for its authentication, which a person changes on the admin page. A local (stdio) connection
/// runs a program on the web server, so it is never created or changed here. The connections the running assistant
/// uses cannot be changed from a chat.
/// </summary>
public sealed class SaveMcpConnectionTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveMcpConnection";

    private static readonly string[] _ownArguments = ["connectionId", "title", "endpoint"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    $$"""
    {
      "type": "object",
      "properties": {
        "connectionId": {
          "type": "string",
          "description": "The ID of the connection to update, from listMcpConnections. Leave it out to create a connection."
        },
        "title": {
          "type": "string",
          "description": "The title of the connection. Required to create one."
        },
        "endpoint": {
          "type": "string",
          "description": "The absolute http or https address of the remote MCP server. Required to create a connection. Confirm it with the user: the tools of the server run with whatever that server allows."
        },
    {{RemoteConnectionAuth.SchemaProperties}}
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a remote (SSE) MCP connection: title, endpoint and authentication. Credentials are never taken or shown: a new connection is anonymous, and a key or secret is entered by a person on the admin page the result links to. Tools of a connected server run with whatever that server allows, so confirm the address with the user. Local-process (stdio) connections cannot be created or changed with this tool.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, McpPermissions.ManageMcpConnections))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<McpConnection>>();
        var isNew = !arguments.TryGetFirstString("connectionId", out var connectionId) || string.IsNullOrWhiteSpace(connectionId);
        McpConnection connection = null;

        if (!isNew)
        {
            connection = await manager.FindByIdAsync(connectionId.Trim(), cancellationToken);

            if (connection is null)
            {
                return $"Nothing was saved: no MCP connection has the ID '{connectionId.Trim()}'. Use listMcpConnections to find it.";
            }
        }

        var credentialArgument = RemoteConnectionAuth.FindCredentialArgument(arguments);

        if (credentialArgument is not null)
        {
            return ConnectionToolSupport.CredentialRefusal(credentialArgument, isNew
                ? ConnectionToolSupport.GetMcpConnectionCreatePath(services, McpConstants.TransportTypes.Sse)
                : ConnectionToolSupport.GetMcpConnectionEditPath(services, connection.ItemId));
        }

        var unknown = arguments.Keys.FirstOrDefault(name => !_ownArguments.Contains(name, StringComparer.Ordinal) && !RemoteConnectionAuth.ArgumentNames.Contains(name, StringComparer.Ordinal));

        if (unknown is not null)
        {
            return $"Nothing was saved: '{unknown}' is not an argument of this tool.";
        }

        if (!isNew && !string.Equals(connection.Source, McpConstants.TransportTypes.Sse, StringComparison.OrdinalIgnoreCase))
        {
            return $"Refused: '{connection.DisplayText}' is a local ({connection.Source}) connection, which runs a program on the web server. Such connections are only created and changed on the admin page: {ConnectionToolSupport.GetMcpConnectionEditPath(services, connection.ItemId)}";
        }

        if (isNew && !services.GetRequiredService<IOptions<McpClientAIOptions>>().Value.TransportTypes.ContainsKey(McpConstants.TransportTypes.Sse))
        {
            return "Nothing was saved: remote (SSE) MCP connections are not available on this site.";
        }

        if (!isNew)
        {
            var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);

            if (protectedConfiguration.IsProtectedMcpConnection(connection.ItemId))
            {
                return ConnectionToolSupport.RunningAssistantRefusal($"the MCP connection '{connection.DisplayText}'", ConnectionToolSupport.GetMcpConnectionEditPath(services, connection.ItemId));
            }
        }

        var stored = connection?.TryGet<SseMcpConnectionMetadata>(out var storedMetadata) == true ? storedMetadata : new SseMcpConnectionMetadata();
        var changes = new List<string>();

        var hasTitle = arguments.TryGetFirstString("title", out var title);
        title = title?.Trim();

        if (isNew || hasTitle)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "Nothing was saved: The Title is required.";
            }
        }

        var hasEndpoint = arguments.TryGetFirstString("endpoint", out var endpointText);
        Uri endpoint = stored.Endpoint;

        if (isNew || hasEndpoint)
        {
            if (string.IsNullOrWhiteSpace(endpointText))
            {
                return "Nothing was saved: Endpoint field is required.";
            }

            // The editor takes any absolute address; the server-sent events transport only speaks http and https.
            if (!Uri.TryCreate(endpointText.Trim(), UriKind.Absolute, out endpoint) || !ConnectionToolSupport.IsWebAddress(endpointText.Trim()))
            {
                return "Nothing was saved: Invalid Endpoint value. Give the absolute http or https address of the MCP server.";
            }

            if (!isNew && RemoteConnectionAuth.MovesCredentials(stored, stored.Endpoint?.ToString(), endpoint.ToString()))
            {
                return $"Nothing was saved: the connection stores credentials, and moving it to another host would send them there. Change the address on the admin page, where the credentials are entered again: {ConnectionToolSupport.GetMcpConnectionEditPath(services, connection.ItemId)}";
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
            return ConnectionToolSupport.NeedsCredentialRefusal(auth.CredentialType, isNew
                ? ConnectionToolSupport.GetMcpConnectionCreatePath(services, McpConstants.TransportTypes.Sse)
                : ConnectionToolSupport.GetMcpConnectionEditPath(services, connection.ItemId), isNew);
        }

        if (isNew)
        {
            connection = await manager.NewAsync(McpConstants.TransportTypes.Sse, new JsonObject(), cancellationToken);
        }

        if (hasTitle && !string.Equals(connection.DisplayText, title, StringComparison.Ordinal))
        {
            changes.Add("title");
        }

        if (hasEndpoint && !Equals(stored.Endpoint, endpoint))
        {
            changes.Add("endpoint");
        }

        changes.AddRange(auth.Changes);

        if (!isNew && changes.Count == 0)
        {
            return AgentToolResults.Json(new
            {
                id = connection.ItemId,
                title = connection.DisplayText,
                changed = changes,
                note = "Nothing was different from the stored connection, so nothing was saved.",
            });
        }

        var metadata = new SseMcpConnectionMetadata { Endpoint = endpoint };
        fields.WriteTo(metadata);

        if (hasTitle)
        {
            connection.DisplayText = title;
        }

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
            // No data, as the editor passes: the connection settings handler rebuilds the authentication from any data
            // object it is given, which would clear the fields set above and protect the stored secrets again.
            await manager.UpdateAsync(connection, data: null, cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            id = connection.ItemId,
            created = isNew,
            title = connection.DisplayText,
            transport = connection.Source,
            endpoint = metadata.Endpoint?.ToString(),
            changed = isNew ? null : changes,
            authentication = RemoteConnectionAuth.Describe(metadata),
            editPath = ConnectionToolSupport.GetMcpConnectionEditPath(services, connection.ItemId),
            note = isNew
                ? "The connection was created with anonymous access. If the server needs a key or other credential, a person enters it on the editPath page. Tick the connection on a profile's Capabilities tab to use its tools."
                : null,
        });
    }
}
