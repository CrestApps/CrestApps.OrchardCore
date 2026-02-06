using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class McpResourceDisplayDriver : DisplayDriver<McpResource>
{
    private readonly McpOptions _mcpOptions;

    internal readonly IStringLocalizer S;

    public McpResourceDisplayDriver(
        IOptions<McpOptions> mcpOptions,
        IStringLocalizer<McpResourceDisplayDriver> stringLocalizer)
    {
        _mcpOptions = mcpOptions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(McpResource entry, BuildDisplayContext context)
    {
        return CombineAsync(
            View("McpResource_Fields_SummaryAdmin", entry).Location("Content:1"),
            View("McpResource_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("McpResource_DefaultMeta_SummaryAdmin", entry).Location("Meta:5"),
            View("McpResource_Description_SummaryAdmin", entry).Location("Description:1")
        );
    }

    public override IDisplayResult Edit(McpResource entry, BuildEditorContext context)
    {
        return Initialize<McpResourceFieldsViewModel>("McpResourceFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Source = entry.Source;
            model.McpPromptItemId = entry.ItemId;
            model.DisplayText = entry.DisplayText;

            // Get the URI patterns from the options for this resource type
            if (!string.IsNullOrEmpty(entry.Source) &&
                _mcpOptions.ResourceTypes.TryGetValue(entry.Source, out var typeEntry) &&
                typeEntry.UriPatterns is not null)
            {
                model.UriPatterns = typeEntry.UriPatterns;
            }

            if (entry.Resource is not null)
            {
                // Extract the path portion from the full URI ({scheme}://{itemId}/{path}).
                if (McpResourceUri.TryParse(entry.Resource.Uri, out var resourceUri))
                {
                    model.Path = resourceUri.Path;
                }

                model.Name = entry.Resource.Name;
                model.Description = entry.Resource.Description;
                model.MimeType = entry.Resource.MimeType;
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpResource entry, UpdateEditorContext context)
    {
        var model = new McpResourceFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The Display Text is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Path))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Path), S["The path is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The Name is required."]);
        }

        entry.DisplayText = model.DisplayText ?? string.Empty;

        entry.Resource ??= new Resource
        {
            Uri = string.Empty,
            Name = string.Empty,
        };

        // Construct the full URI from the protocol (source type), item ID, and user-provided path.
        entry.Resource.Uri = McpResourceUri.Build(entry.Source, entry.ItemId, model.Path);

        entry.Resource.Name = model.Name ?? string.Empty;
        entry.Resource.Title = entry.DisplayText;
        entry.Resource.Description = model.Description;
        entry.Resource.MimeType = model.MimeType;

        return Edit(entry, context);
    }
}
