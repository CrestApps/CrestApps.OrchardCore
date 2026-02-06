using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class McpPromptDisplayDriver : DisplayDriver<McpPrompt>
{
    internal readonly IStringLocalizer S;

    public McpPromptDisplayDriver(IStringLocalizer<McpPromptDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(McpPrompt entry, BuildDisplayContext context)
    {
        return CombineAsync(
            View("McpPrompt_Fields_SummaryAdmin", entry).Location("Content:1"),
            View("McpPrompt_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("McpPrompt_DefaultMeta_SummaryAdmin", entry).Location("Meta:5"),
            View("McpPrompt_Description_SummaryAdmin", entry).Location("Description:1")
        );
    }

    public override IDisplayResult Edit(McpPrompt entry, BuildEditorContext context)
    {
        return Initialize<McpPromptFieldsViewModel>("McpPromptFields_Edit", model =>
        {
            model.IsNew = context.IsNew;

            model.Name = entry.Name;

            if (entry.Prompt is not null)
            {
                model.Title = entry.Prompt.Title;
                model.Description = entry.Prompt.Description;
                model.Arguments = entry.Prompt.Arguments?.Select(a => new McpPromptArgumentViewModel
                {
                    Name = a.Name,
                    Title = a.Title,
                    Description = a.Description,
                    Required = a.Required ?? false,
                }).ToList() ?? [];
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpPrompt entry, UpdateEditorContext context)
    {
        var model = new McpPromptFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The Name is required."]);
        }

        // Validate arguments if provided
        var validArguments = model.Arguments?
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .ToList() ?? [];

        var name = model.Name ?? string.Empty;

        entry.Name = name;
        entry.Prompt ??= new Prompt
        {
            Name = name,
        };
        entry.Prompt.Name = name;
        entry.Prompt.Title = model.Title;
        entry.Prompt.Description = model.Description;

        entry.Prompt.Arguments = validArguments.Select(a => new PromptArgument
        {
            Name = a.Name,
            Title = a.Title,
            Description = a.Description,
            Required = a.Required,
        }).ToList();

        return Edit(entry, context);
    }
}
