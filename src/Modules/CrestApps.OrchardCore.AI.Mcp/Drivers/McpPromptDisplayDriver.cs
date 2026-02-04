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

    public override Task<IDisplayResult> DisplayAsync(McpPrompt prompt, BuildDisplayContext context)
    {
        return CombineAsync(
            View("McpPrompt_Fields_SummaryAdmin", prompt).Location("Content:1"),
            View("McpPrompt_Buttons_SummaryAdmin", prompt).Location("Actions:5"),
            View("McpPrompt_DefaultMeta_SummaryAdmin", prompt).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(McpPrompt entry, BuildEditorContext context)
    {
        return Initialize<McpPromptFieldsViewModel>("McpPromptFields_Edit", model =>
        {
            model.Name = entry.Prompt?.Name;
            model.Title = entry.Prompt?.Title;
            model.Description = entry.Prompt?.Description;
            model.Arguments = entry.Prompt?.Arguments?.Select(a => new McpPromptArgumentViewModel
            {
                Name = a.Name,
                Title = a.Title,
                Description = a.Description,
                Required = a.Required ?? false,
            }).ToList() ?? [];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpPrompt entry, UpdateEditorContext context)
    {
        var model = new McpPromptFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Title), S["The Title is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The Name is required."]);
        }

        // Validate arguments if provided
        var validArguments = model.Arguments?
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .ToList() ?? [];

        entry.Prompt ??= new Prompt { Name = string.Empty };
        entry.Prompt.Name = model.Name ?? string.Empty;
        entry.Prompt.Title = model.Title;
        entry.Prompt.Description = model.Description;

        entry.Prompt.Arguments = validArguments.Select(a => new PromptArgument
        {
            Name = a.Name ?? string.Empty,
            Title = a.Title,
            Description = a.Description,
            Required = a.Required,
        }).ToList();

        return Edit(entry, context);
    }
}
