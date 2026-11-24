using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Connections;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Agent.McpServer;

/// <summary>
/// Creates or updates a resource of the site's MCP server through the resource manager, as the resource editor does:
/// the URI is the type, the resource's ID and the path a person enters. An FTP or SFTP resource needs a host and
/// credentials, which the assistant never takes, so it is only made on the admin page; its title, name, path,
/// description and MIME type can be changed here, its connection cannot.
/// </summary>
public sealed class SaveMcpResourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveMcpResource";

    private static readonly string[] _arguments = ["resourceId", "type", "title", "name", "path", "description", "mimeType"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "resourceId": {
          "type": "string",
          "description": "The ID of the resource to update, from listMcpResources. Leave it out to create a resource."
        },
        "type": {
          "type": "string",
          "description": "The resource type, from listMcpResources with includeTypes, for example file, media, content-by-id or content-by-type. Required to create a resource; it cannot be changed."
        },
        "title": {
          "type": "string",
          "description": "The title shown to people and MCP clients. Required to create a resource."
        },
        "name": {
          "type": "string",
          "description": "The name MCP clients list the resource by. Required to create a resource."
        },
        "path": {
          "type": "string",
          "description": "The path after the resource's own address, with the type's variables in braces, for example {providerName}/{fileName}. Empty for the resource itself."
        },
        "description": {
          "type": "string",
          "description": "What the resource gives. An empty text clears it."
        },
        "mimeType": {
          "type": "string",
          "description": "The MIME type of the content, for example text/markdown. An empty text clears it."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates an MCP server resource of a given type and URI pattern. A resource makes media, content or a remote FTP/SFTP folder readable by MCP clients, so confirm what it exposes. Credentials are never taken: FTP and SFTP resources, and file resources (which read files of the server), are created and changed on the admin page the result links to.";

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

        var manager = services.GetRequiredService<ISourceCatalogManager<McpResource>>();
        var isNew = !arguments.TryGetFirstString("resourceId", out var resourceId) || string.IsNullOrWhiteSpace(resourceId);
        McpResource resource = null;

        if (!isNew)
        {
            resource = await manager.FindByIdAsync(resourceId.Trim(), cancellationToken);

            if (resource is null)
            {
                return $"Nothing was saved: no MCP resource has the ID '{resourceId.Trim()}'. Use listMcpResources to find it.";
            }
        }

        var credentialArgument = RemoteConnectionAuth.FindCredentialArgument(arguments);

        if (credentialArgument is not null)
        {
            return ConnectionToolSupport.CredentialRefusal(credentialArgument, isNew ? null : McpServerToolSupport.GetResourceEditPath(services, resource.ItemId));
        }

        var unknown = arguments.Keys.FirstOrDefault(name => !_arguments.Contains(name, StringComparer.Ordinal));

        if (unknown is not null)
        {
            return $"Nothing was saved: '{unknown}' is not an argument of this tool. The connection of an FTP or SFTP resource is changed on the admin page.";
        }

        var hasType = arguments.TryGetFirstString("type", out var type);
        type = type?.Trim();

        if (isNew)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return "Nothing was saved: give the resource type in type. listMcpResources with includeTypes lists them.";
            }

            var resourceTypes = services.GetRequiredService<IOptions<McpOptions>>().Value.ResourceTypes;

            if (!resourceTypes.TryGetValue(type, out var entry))
            {
                return $"Nothing was saved: '{type}' is not a resource type of this site. listMcpResources with includeTypes lists them.";
            }

            type = entry.Type;

            if (McpServerToolSupport.IsRemoteType(type))
            {
                var createPath = McpServerToolSupport.GetResourceCreatePath(services, type);

                return $"Nothing was saved: an {type.ToUpperInvariant()} resource needs a host and credentials, which the assistant never takes. Create it on the admin page{(string.IsNullOrEmpty(createPath) ? "." : $": {createPath}")}";
            }

            // A file resource hands the files of the server's own file providers to every MCP client, so it is never
            // created from inside an AI turn, as a stdio connection is not (review of pull request 1334, F4).
            if (string.Equals(type, McpServerToolSupport.FileResourceType, StringComparison.OrdinalIgnoreCase))
            {
                var createPath = McpServerToolSupport.GetResourceCreatePath(services, type);

                return $"Nothing was saved: a file resource lets MCP clients read files of the server, so it is not created from the chat. A person creates it on the admin page{(string.IsNullOrEmpty(createPath) ? "." : $": {createPath}")}";
            }
        }
        else if (hasType && !string.Equals(type, resource.Source, StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was saved: the type of a resource cannot be changed. Create a new resource of the other type instead.";
        }
        else if (string.Equals(resource.Source, McpServerToolSupport.FileResourceType, StringComparison.OrdinalIgnoreCase))
        {
            var editPath = McpServerToolSupport.GetResourceEditPath(services, resource.ItemId);

            return $"Nothing was saved: a file resource lets MCP clients read files of the server, so it is not changed from the chat. A person changes it on the admin page{(string.IsNullOrEmpty(editPath) ? "." : $": {editPath}")}";
        }

        var hasTitle = arguments.TryGetFirstString("title", out var title);
        var hasName = arguments.TryGetFirstString("name", out var name);
        var hasPath = arguments.TryGetFirstString("path", true, out var path);
        var hasDescription = arguments.TryGetFirstString("description", true, out var description);
        var hasMimeType = arguments.TryGetFirstString("mimeType", true, out var mimeType);
        title = title?.Trim();
        name = name?.Trim();

        if ((isNew || hasTitle) && string.IsNullOrWhiteSpace(title))
        {
            return "Nothing was saved: The Display Text is required.";
        }

        if ((isNew || hasName) && string.IsNullOrWhiteSpace(name))
        {
            return "Nothing was saved: The Name is required.";
        }

        resource ??= await manager.NewAsync(type, new JsonObject(), cancellationToken);
        resource.Resource ??= new Resource
        {
            Uri = string.Empty,
            Name = string.Empty,
        };

        var currentPath = McpResourceUris.ExtractPath(resource.Resource.Uri, resource.Source, resource.ItemId);
        var changes = new List<string>();

        if (hasTitle && !string.Equals(resource.DisplayText, title, StringComparison.Ordinal))
        {
            resource.DisplayText = title;
            changes.Add("title");
        }

        if (hasName && !string.Equals(resource.Resource.Name, name, StringComparison.Ordinal))
        {
            resource.Resource.Name = name;
            changes.Add("name");
        }

        if (isNew || hasPath)
        {
            var uri = McpResourceUris.Build(resource.Source, resource.ItemId, path?.Trim());

            if (!string.Equals(resource.Resource.Uri, uri, StringComparison.Ordinal))
            {
                resource.Resource.Uri = uri;
                changes.Add("path");
            }
        }

        if (hasDescription)
        {
            var value = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            if (!string.Equals(resource.Resource.Description, value, StringComparison.Ordinal))
            {
                resource.Resource.Description = value;
                changes.Add("description");
            }
        }

        if (hasMimeType)
        {
            var value = string.IsNullOrWhiteSpace(mimeType) ? null : mimeType.Trim();

            if (!string.Equals(resource.Resource.MimeType, value, StringComparison.Ordinal))
            {
                resource.Resource.MimeType = value;
                changes.Add("mimeType");
            }
        }

        // The title MCP clients see is always the display text, as the editor keeps it.
        resource.Resource.Title = resource.DisplayText;

        if (!isNew && changes.Count == 0)
        {
            return AgentToolResults.Json(new
            {
                id = resource.ItemId,
                title = resource.DisplayText,
                changed = changes,
                note = "Nothing was different from the stored resource, so nothing was saved.",
            });
        }

        var validation = await manager.ValidateAsync(resource, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}";
        }

        if (isNew)
        {
            await manager.CreateAsync(resource, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(resource, new JsonObject(), cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            id = resource.ItemId,
            created = isNew,
            title = resource.DisplayText,
            type = resource.Source,
            name = resource.Resource.Name,
            uri = resource.Resource.Uri,
            previousPath = isNew || !changes.Contains("path") ? null : currentPath,
            changed = isNew ? null : changes,
            editPath = McpServerToolSupport.GetResourceEditPath(services, resource.ItemId),
        });
    }
}
