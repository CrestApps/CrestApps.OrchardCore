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
/// Imports MCP resources, matched by their item id. An update applies the display text and resource fields the recipe
/// carries.
/// </summary>
internal sealed class McpResourceStep : CatalogRecipeStepBase<McpResource>
{
    public const string StepKey = "McpResource";

    private readonly ISourceCatalogManager<McpResource> _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpResourceStep"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpResourceStep(
        ISourceCatalogManager<McpResource> manager,
        IStringLocalizer<McpResourceStep> stringLocalizer)
        : base(StepKey, "Resources")
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override bool ValidateUpdatedEntries => false;

    protected override ValueTask<McpResource> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override ValueTask UpdateAsync(McpResource entry, JsonObject token)
    {
        PopulateEntry(entry, token);

        return _manager.UpdateAsync(entry);
    }

    protected override async ValueTask<McpResource> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = token[nameof(McpResource.Source)]?.GetValue<string>();

        if (string.IsNullOrEmpty(source))
        {
            context.Errors.Add(S["Resource source type is required."]);

            return null;
        }

        var entry = await _manager.NewAsync(source, token);
        PopulateEntry(entry, token);

        return entry;
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(McpResource entry)
        => _manager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(McpResource entry)
        => _manager.CreateAsync(entry);

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

        // Always derive the MCP resource title from the display text.
        if (entry.Resource is not null)
        {
            entry.Resource.Title = entry.DisplayText;
        }
    }
}
