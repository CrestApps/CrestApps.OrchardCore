using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Recipes;

/// <summary>
/// Imports MCP prompts, matched by their item id. An update applies the fields the recipe carries.
/// </summary>
internal sealed class McpPromptStep : CatalogRecipeStepBase<McpPrompt>
{
    public const string StepKey = "McpPrompt";

    private readonly INamedCatalogManager<McpPrompt> _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpPromptStep"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpPromptStep(
        INamedCatalogManager<McpPrompt> manager,
        IStringLocalizer<McpPromptStep> stringLocalizer)
        : base(StepKey, "Prompts")
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override bool ValidateUpdatedEntries => false;

    protected override ValueTask<McpPrompt> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override ValueTask UpdateAsync(McpPrompt entry, JsonObject token)
    {
        PopulateEntry(entry, token);

        return _manager.UpdateAsync(entry);
    }

    protected override async ValueTask<McpPrompt> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var entry = await _manager.NewAsync(token);
        PopulateEntry(entry, token);

        return entry;
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(McpPrompt entry)
        => _manager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(McpPrompt entry)
        => _manager.CreateAsync(entry);

    private static void PopulateEntry(McpPrompt entry, JsonObject token)
    {
        // Populate the Prompt from token
        var promptData = token[nameof(McpPrompt.Prompt)]?.AsObject();

        var name = promptData[nameof(McpPrompt.Name)]?.GetValue<string>();

        entry.Name = name;

        if (promptData is not null)
        {
            entry.Prompt ??= new Prompt
            {
                Name = string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(name))
            {
                entry.Prompt.Name = name;
            }

            var title = promptData[nameof(Prompt.Title)]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(title))
            {
                entry.Prompt.Title = title;
            }

            var description = promptData[nameof(Prompt.Description)]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(description))
            {
                entry.Prompt.Description = description;
            }

            var argumentsArray = promptData[nameof(Prompt.Arguments)]?.AsArray();
            if (argumentsArray is not null)
            {
                entry.Prompt.Arguments = argumentsArray
                    .Where(a => a is not null)
                    .Select(a =>
                    {
                        var obj = a.AsObject();
                        return new PromptArgument
                        {
                            Name = obj[nameof(PromptArgument.Name)]?.GetValue<string>() ?? string.Empty,
                            Title = obj[nameof(PromptArgument.Title)]?.GetValue<string>(),
                            Description = obj[nameof(PromptArgument.Description)]?.GetValue<string>(),
                            Required = obj[nameof(PromptArgument.Required)]?.GetValue<bool>(),
                        };
                    })
                    .ToList();
            }
        }

        var messagesArray = token[nameof(McpPrompt.Messages)]?.AsArray();
        if (messagesArray is not null)
        {
            entry.Messages = messagesArray
                .Where(m => m is not null)
                .Select(m =>
                {
                    var obj = m.AsObject();
                    return new McpPromptMessage
                    {
                        Role = obj[nameof(McpPromptMessage.Role)]?.GetValue<string>(),
                        Content = obj[nameof(McpPromptMessage.Content)]?.GetValue<string>(),
                    };
                })
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .ToList();
        }
    }
}
