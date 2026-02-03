using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Recipes;

[Feature(McpConstants.Feature.Server)]
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
            McpPrompt prompt = null;

            var id = token[nameof(McpPrompt.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                prompt = await _manager.FindByIdAsync(id);
            }

            if (prompt is not null)
            {
                // Update existing prompt
                PopulatePrompt(prompt, token);
                await _manager.UpdateAsync(prompt);
            }
            else
            {
                // Create new prompt
                prompt = await _manager.NewAsync(token);
                PopulatePrompt(prompt, token);

                if (hasId && IdValidator.IsValid(id))
                {
                    prompt.ItemId = id;
                }

                var validationResult = await _manager.ValidateAsync(prompt);

                if (!validationResult.Succeeded)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        context.Errors.Add(error.ErrorMessage);
                    }

                    continue;
                }

                await _manager.CreateAsync(prompt);
            }
        }
    }

    private static void PopulatePrompt(McpPrompt prompt, JsonObject token)
    {
        var displayText = token[nameof(McpPrompt.DisplayText)]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(displayText))
        {
            prompt.DisplayText = displayText;
        }

        var name = token[nameof(McpPrompt.Name)]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(name))
        {
            prompt.Name = name;
        }

        var description = token[nameof(McpPrompt.Description)]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(description))
        {
            prompt.Description = description;
        }

        var argumentsArray = token[nameof(McpPrompt.Arguments)]?.AsArray();
        if (argumentsArray is not null)
        {
            prompt.Arguments = argumentsArray
                .Where(a => a is not null)
                .Select(a =>
                {
                    var obj = a.AsObject();
                    return new McpPromptArgument
                    {
                        Name = obj[nameof(McpPromptArgument.Name)]?.GetValue<string>(),
                        Description = obj[nameof(McpPromptArgument.Description)]?.GetValue<string>(),
                        IsRequired = obj[nameof(McpPromptArgument.IsRequired)]?.GetValue<bool>() ?? false,
                    };
                })
                .ToList();
        }

        var messagesArray = token[nameof(McpPrompt.Messages)]?.AsArray();
        if (messagesArray is not null)
        {
            prompt.Messages = messagesArray
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
                .ToList();
        }
    }

    private sealed class McpPromptDeploymentStepModel
    {
        public JsonArray Prompts { get; set; }
    }
}
