using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.McpServer;

/// <summary>
/// Lists the prompts the site's MCP server offers, as the MCP Prompts page does, with their arguments and messages.
/// </summary>
public sealed class ListMcpPromptsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listMcpPrompts";

    private const int MaxMessageLength = 4000;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Only prompts whose name or title contains this text."
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
          "description": "How many prompts per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the prompts the site's MCP server offers, with their arguments and messages.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, McpPermissions.ManageMcpPrompts))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        IEnumerable<McpPrompt> prompts = await services.GetRequiredService<INamedCatalogManager<McpPrompt>>().GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            prompts = prompts.Where(prompt =>
                prompt.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true ||
                prompt.Prompt?.Title?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
        }

        var filtered = prompts
            .OrderBy(prompt => prompt.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(prompt => new
            {
                id = prompt.ItemId,
                name = prompt.Name,
                title = prompt.Prompt?.Title,
                description = prompt.Prompt?.Description,
                arguments = (prompt.Prompt?.Arguments ?? [])
                    .Select(argument => new
                    {
                        name = argument.Name,
                        title = argument.Title,
                        description = argument.Description,
                        required = argument.Required == true,
                    })
                    .ToList(),
                messages = (prompt.Messages ?? [])
                    .Select(message => new
                    {
                        role = message.Role,
                        content = message.Content is { Length: > MaxMessageLength } content ? content[..MaxMessageLength] : message.Content,
                        truncated = message.Content?.Length > MaxMessageLength ? true : (bool?)null,
                    })
                    .ToList(),
                editPath = McpServerToolSupport.GetPromptEditPath(services, prompt.ItemId),
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize,
        });
    }
}
