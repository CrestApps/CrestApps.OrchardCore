using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.McpServer;

/// <summary>
/// Lists the resources the site's MCP server offers, as the MCP Resources page does. For an FTP or SFTP resource the
/// host, port and user name are shown, and its password, private key and passphrase only as stored or not.
/// </summary>
public sealed class ListMcpResourcesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listMcpResources";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Only resources whose title or name contains this text."
        },
        "type": {
          "type": "string",
          "description": "Only resources of this type, for example file, media, content-by-id or ftp."
        },
        "includeTypes": {
          "type": "boolean",
          "description": "Also list the resource types a resource can be made of, with the URI variables each supports. Defaults to false."
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
          "description": "How many resources per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the resources the site's MCP server offers, with type, URI pattern and MIME type, and whether credentials are stored. With includeTypes it lists the resource types and the URI variables each supports. Credentials are never shown.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, McpPermissions.ManageMcpResources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("type", out var type);
        var includeTypes = arguments.GetFirstValueOrDefault("includeTypes", false);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        IEnumerable<McpResource> resources = await services.GetRequiredService<ISourceCatalogManager<McpResource>>().GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            resources = resources.Where(resource =>
                resource.DisplayText?.Contains(text, StringComparison.OrdinalIgnoreCase) == true ||
                resource.Resource?.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            resources = resources.Where(resource => string.Equals(resource.Source, type.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var filtered = resources
            .OrderBy(resource => resource.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(resource => new
            {
                id = resource.ItemId,
                title = resource.DisplayText,
                type = resource.Source,
                name = resource.Resource?.Name,
                uri = resource.Resource?.Uri,
                path = McpResourceUris.ExtractPath(resource.Resource?.Uri, resource.Source, resource.ItemId),
                description = resource.Resource?.Description,
                mimeType = resource.Resource?.MimeType,
                remoteFolder = ReadRemoteFolder(resource),
                editPath = McpServerToolSupport.GetResourceEditPath(services, resource.ItemId),
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize,
            types = includeTypes
                ? services.GetRequiredService<IOptions<McpOptions>>().Value.ResourceTypes.Values
                    .OrderBy(entry => entry.Type, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new
                    {
                        type = entry.Type,
                        displayName = entry.DisplayName?.Value,
                        description = entry.Description?.Value,
                        variables = (entry.SupportedVariables ?? [])
                            .Select(variable => new { name = variable.Name, description = variable.Description?.Value })
                            .ToList(),
                        needsCredentials = McpServerToolSupport.IsRemoteType(entry.Type),
                    })
                    .ToList()
                : null,
        });
    }

    private static object ReadRemoteFolder(McpResource resource)
    {
        foreach (var key in McpServerToolSupport.RemoteConnectionKeys)
        {
            var connection = resource.Get<RemoteFolderFields>(key);

            if (connection is not null)
            {
                return new
                {
                    host = connection.Host,
                    port = connection.Port,
                    username = connection.Username,
                    hasPassword = !string.IsNullOrEmpty(connection.Password),
                    hasPrivateKey = !string.IsNullOrEmpty(connection.PrivateKey),
                    hasPassphrase = !string.IsNullOrEmpty(connection.Passphrase),
                };
            }
        }

        return null;
    }

    /// <summary>
    /// The fields of an FTP or SFTP resource's connection. The secrets are only read to tell whether they are stored.
    /// </summary>
    private sealed class RemoteFolderFields
    {
        public string Host { get; set; }

        public int? Port { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string PrivateKey { get; set; }

        public string Passphrase { get; set; }
    }
}
