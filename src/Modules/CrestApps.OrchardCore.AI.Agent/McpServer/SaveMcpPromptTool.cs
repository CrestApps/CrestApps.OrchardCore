using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Agent.McpServer;

/// <summary>
/// Creates or updates a prompt of the site's MCP server through the prompt manager, as the prompt editor does:
/// arguments without a name and messages without content are left out, a message without a role is a user message,
/// and the prompt handler checks the rest.
/// </summary>
public sealed class SaveMcpPromptTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveMcpPrompt";

    private const int MaxItems = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "promptId": {
          "type": "string",
          "description": "The ID of the prompt to update, from listMcpPrompts. Leave it out to create a prompt."
        },
        "name": {
          "type": "string",
          "description": "The name MCP clients ask for the prompt by, for example summarize-article. Required to create a prompt."
        },
        "title": {
          "type": "string",
          "description": "The title shown to people. Required to create a prompt."
        },
        "description": {
          "type": "string",
          "description": "What the prompt is for. An empty text clears it."
        },
        "arguments": {
          "type": "array",
          "maxItems": 50,
          "description": "The arguments an MCP client fills in. Replaces the current list.",
          "items": {
            "type": "object",
            "properties": {
              "name": { "type": "string", "description": "The argument's name, for example contentItemId." },
              "title": { "type": "string", "description": "The title shown to people." },
              "description": { "type": "string", "description": "What to give." },
              "required": { "type": "boolean", "description": "Whether the argument must be given." }
            },
            "required": ["name"],
            "additionalProperties": false
          }
        },
        "messages": {
          "type": "array",
          "maxItems": 50,
          "description": "The messages of the prompt, in order. Replaces the current list. Arguments are written in a message as {{name}}.",
          "items": {
            "type": "object",
            "properties": {
              "role": { "type": "string", "enum": ["user", "assistant"], "description": "Who says the message. Defaults to user." },
              "content": { "type": "string", "description": "The text of the message." }
            },
            "required": ["content"],
            "additionalProperties": false
          }
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates an MCP server prompt: name, title, description, arguments and the user and assistant messages. Lists given replace the current ones.";

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

        var manager = services.GetRequiredService<INamedCatalogManager<McpPrompt>>();
        var isNew = !arguments.TryGetFirstString("promptId", out var promptId) || string.IsNullOrWhiteSpace(promptId);
        McpPrompt prompt = null;

        if (!isNew)
        {
            prompt = await manager.FindByIdAsync(promptId.Trim(), cancellationToken);

            if (prompt is null)
            {
                return $"Nothing was saved: no MCP prompt has the ID '{promptId.Trim()}'. Use listMcpPrompts to find it.";
            }
        }

        var hasName = arguments.TryGetFirstString("name", out var name);
        var hasTitle = arguments.TryGetFirstString("title", out var title);
        var hasDescription = arguments.TryGetFirstString("description", true, out var description);
        name = name?.Trim();
        title = title?.Trim();

        if ((isNew || hasName) && string.IsNullOrWhiteSpace(name))
        {
            return "Nothing was saved: The Name is required.";
        }

        if ((isNew || hasTitle) && string.IsNullOrWhiteSpace(title))
        {
            return "Nothing was saved: Title is required.";
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "arguments", out var argumentItems) ||
            !AgentToolArguments.TryReadJsonArray(arguments, "messages", out var messageItems))
        {
            return "Nothing was saved: arguments and messages must each be a list of objects.";
        }

        if (argumentItems?.Count > MaxItems || messageItems?.Count > MaxItems)
        {
            return $"Nothing was saved: a prompt takes at most {MaxItems} arguments and {MaxItems} messages.";
        }

        List<PromptArgument> promptArguments = null;

        if (argumentItems is not null)
        {
            promptArguments = [];

            foreach (var item in argumentItems)
            {
                if (item is not JsonObject argument)
                {
                    return "Nothing was saved: each argument must be an object with a name.";
                }

                var argumentName = ReadText(argument, "name");

                // The editor leaves out rows without a name.
                if (argumentName is null)
                {
                    continue;
                }

                promptArguments.Add(new PromptArgument
                {
                    Name = argumentName,
                    Title = ReadText(argument, "title"),
                    Description = ReadText(argument, "description"),
                    Required = argument["required"] is JsonValue required && required.TryGetValue<bool>(out var isRequired) && isRequired,
                });
            }
        }

        List<McpPromptMessage> messages = null;

        if (messageItems is not null)
        {
            messages = [];

            foreach (var item in messageItems)
            {
                if (item is not JsonObject message)
                {
                    return "Nothing was saved: each message must be an object with content.";
                }

                var content = message["content"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

                // The editor leaves out rows without content.
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                var role = ReadText(message, "role");

                messages.Add(new McpPromptMessage
                {
                    Role = role is null ? "user" : role.ToLowerInvariant(),
                    Content = content,
                });
            }
        }

        if (hasName && !string.IsNullOrWhiteSpace(name))
        {
            var sameName = await manager.FindByNameAsync(name, cancellationToken);

            if (sameName is not null && !string.Equals(sameName.ItemId, prompt?.ItemId, StringComparison.Ordinal))
            {
                return $"Nothing was saved: another MCP prompt is already named '{name}'.";
            }
        }

        prompt ??= await manager.NewAsync(new JsonObject(), cancellationToken);

        if (hasName)
        {
            prompt.Name = name;
        }

        prompt.Prompt ??= new Prompt { Name = prompt.Name };
        prompt.Prompt.Name = prompt.Name;

        if (hasTitle)
        {
            prompt.Prompt.Title = title;
        }

        if (hasDescription)
        {
            prompt.Prompt.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }

        if (promptArguments is not null)
        {
            prompt.Prompt.Arguments = promptArguments;
        }

        if (messages is not null)
        {
            prompt.Messages = messages;
        }

        var validation = await manager.ValidateAsync(prompt, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}";
        }

        if (isNew)
        {
            await manager.CreateAsync(prompt, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(prompt, new JsonObject(), cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            id = prompt.ItemId,
            created = isNew,
            name = prompt.Name,
            title = prompt.Prompt.Title,
            arguments = (prompt.Prompt.Arguments ?? []).Select(argument => argument.Name).ToList(),
            messages = prompt.Messages?.Count ?? 0,
            editPath = McpServerToolSupport.GetPromptEditPath(services, prompt.ItemId),
        });
    }

    private static string ReadText(JsonObject json, string key)
        => json[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
