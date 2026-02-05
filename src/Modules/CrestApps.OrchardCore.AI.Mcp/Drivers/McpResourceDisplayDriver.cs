using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class McpResourceDisplayDriver : DisplayDriver<McpResource>
{
    private readonly IMcpResourceStore _store;

    internal readonly IStringLocalizer S;

    public McpResourceDisplayDriver(
        IMcpResourceStore store,
        IStringLocalizer<McpResourceDisplayDriver> stringLocalizer)
    {
        _store = store;
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
            model.DisplayText = entry.DisplayText;

            if (entry.Resource is not null)
            {
                model.Uri = entry.Resource.Uri;
                model.Name = entry.Resource.Name;
                model.Title = entry.Resource.Title;
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

        if (string.IsNullOrWhiteSpace(model.Uri))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Uri), S["The URI is required."]);
        }
        else
        {
            // Validate URI uniqueness using efficient lookup
            var duplicate = await _store.FindByUriAsync(model.Uri);

            if (duplicate is not null && duplicate.ItemId != entry.ItemId)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Uri), S["A resource with the URI '{0}' already exists.", model.Uri]);
            }
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
        entry.Resource.Uri = model.Uri ?? string.Empty;
        entry.Resource.Name = model.Name ?? string.Empty;
        entry.Resource.Title = model.Title;
        entry.Resource.Description = model.Description;
        entry.Resource.MimeType = model.MimeType;

        return Edit(entry, context);
    }
}
