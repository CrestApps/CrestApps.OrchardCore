using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Recipes;

internal sealed class McpPromptStep : NamedRecipeStepHandler
{
    public const string StepKey = "McpPrompt";

    private readonly ICatalogManager<McpPrompt> _manager;

    internal readonly IStringLocalizer S;

    public McpPromptStep(
        ICatalogManager<McpPrompt> manager,
        IStringLocalizer<McpPromptStep> stringLocalizer)
         : base(StepKey)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<McpPromptDeploymentStepModel>();
        var tokens = model.Prompts.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            var id = token[nameof(McpPrompt.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            McpPrompt entry = hasId ? await _manager.FindByIdAsync(id) : null;

            if (entry is not null)
            {
                // Update existing prompt
                PopulateEntry(entry, token);
                await _manager.UpdateAsync(entry);
            }
            else
            {
                // Create new prompt
                entry = await _manager.NewAsync(token);
                PopulateEntry(entry, token);

                if (hasId && IdValidator.IsValid(id))
                {
                    entry.ItemId = id;
                }

                var validationResult = await _manager.ValidateAsync(entry);

                if (!validationResult.Succeeded)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        context.Errors.Add(error.ErrorMessage);
                    }

                    continue;
                }

                await _manager.CreateAsync(entry);
            }
        }
    }

    private static void PopulateEntry(McpPrompt entry, JsonObject token)
    {
        // Populate the Prompt from token
        var promptData = token[nameof(McpPrompt.Prompt)]?.AsObject();
        if (promptData is not null)
        {
            entry.Prompt ??= new Prompt { Name = string.Empty };

            var name = promptData[nameof(Prompt.Name)]?.GetValue<string>();
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
    }

    private sealed class McpPromptDeploymentStepModel
    {
        public JsonArray Prompts { get; set; }
    }
}
