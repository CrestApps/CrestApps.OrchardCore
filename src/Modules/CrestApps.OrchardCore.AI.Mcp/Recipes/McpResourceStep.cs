using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Recipes;

internal sealed class McpResourceStep : NamedRecipeStepHandler
{
    public const string StepKey = "McpResource";

    private readonly ISourceCatalogManager<McpResource> _manager;

    internal readonly IStringLocalizer S;

    public McpResourceStep(
        ISourceCatalogManager<McpResource> manager,
        IStringLocalizer<McpResourceStep> stringLocalizer)
         : base(StepKey)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<McpResourceDeploymentStepModel>();
        var tokens = model.Resources.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            var id = token[nameof(McpResource.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            McpResource entry = hasId ? await _manager.FindByIdAsync(id) : null;

            if (entry is not null)
            {
                // Update existing resource
                PopulateEntry(entry, token);
                await _manager.UpdateAsync(entry);
            }
            else
            {
                // Create new resource
                var source = token[nameof(McpResource.Source)]?.GetValue<string>();

                if (string.IsNullOrEmpty(source))
                {
                    context.Errors.Add(S["Resource source type is required."]);
                    continue;
                }

                entry = await _manager.NewAsync(source, token);
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

    private static void PopulateEntry(McpResource entry, JsonObject token)
    {
        var displayText = token[nameof(McpResource.DisplayText)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            entry.DisplayText = displayText;
        }

        // Populate the Resource from token
        var resourceData = token[nameof(McpResource.Resource)]?.AsObject();

        if (resourceData is not null)
        {
            entry.Resource ??= new Resource
            {
                Uri = string.Empty,
                Name = string.Empty,
            };

            var uri = resourceData[nameof(Resource.Uri)]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(uri))
            {
                entry.Resource.Uri = uri;
            }

            var name = resourceData[nameof(Resource.Name)]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(name))
            {
                entry.Resource.Name = name;
            }

            var title = resourceData[nameof(Resource.Title)]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(title))
            {
                entry.Resource.Title = title;
            }

            var description = resourceData[nameof(Resource.Description)]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(description))
            {
                entry.Resource.Description = description;
            }

            var mimeType = resourceData[nameof(Resource.MimeType)]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(mimeType))
            {
                entry.Resource.MimeType = mimeType;
            }
        }
    }

    private sealed class McpResourceDeploymentStepModel
    {
        public JsonArray Resources { get; set; }
    }
}
