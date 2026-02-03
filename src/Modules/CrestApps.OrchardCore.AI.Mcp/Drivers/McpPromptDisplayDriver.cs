using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.Extensions.Localization;
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

    public override IDisplayResult Edit(McpPrompt prompt, BuildEditorContext context)
    {
        return Initialize<McpPromptFieldsViewModel>("McpPromptFields_Edit", model =>
        {
            model.DisplayText = prompt.DisplayText;
            model.Name = prompt.Name;
            model.Description = prompt.Description;
            model.Arguments = prompt.Arguments?.Select(a => new McpPromptArgumentViewModel
            {
                Name = a.Name,
                Description = a.Description,
                IsRequired = a.IsRequired,
            }).ToList() ?? [];
            model.Messages = prompt.Messages?.Select(m => new McpPromptMessageViewModel
            {
                Role = m.Role,
                Content = m.Content,
            }).ToList() ?? [];

            // Ensure at least one message exists for new prompts
            if (model.Messages.Count == 0)
            {
                model.Messages.Add(new McpPromptMessageViewModel
                {
                    Role = McpConstants.Roles.User,
                    Content = string.Empty,
                });
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpPrompt prompt, UpdateEditorContext context)
    {
        var model = new McpPromptFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The Title is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The Name is required."]);
        }

        // Filter out empty messages
        var validMessages = model.Messages?
            .Where(m => !string.IsNullOrWhiteSpace(m.Role) || !string.IsNullOrWhiteSpace(m.Content))
            .ToList() ?? [];

        if (validMessages.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Messages), S["At least one message is required."]);
        }
        else
        {
            for (int i = 0; i < validMessages.Count; i++)
            {
                var message = validMessages[i];
                if (string.IsNullOrWhiteSpace(message.Role))
                {
                    context.Updater.ModelState.AddModelError(Prefix, $"Messages[{i}].Role", S["Message {0} requires a role.", i + 1]);
                }
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    context.Updater.ModelState.AddModelError(Prefix, $"Messages[{i}].Content", S["Message {0} requires content.", i + 1]);
                }
            }
        }

        // Validate arguments if provided
        var validArguments = model.Arguments?
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .ToList() ?? [];

        prompt.DisplayText = model.DisplayText;
        prompt.Name = model.Name;
        prompt.Description = model.Description;

        prompt.Arguments = validArguments.Select(a => new McpPromptArgument
        {
            Name = a.Name,
            Description = a.Description,
            IsRequired = a.IsRequired,
        }).ToList();

        prompt.Messages = validMessages.Select(m => new McpPromptMessage
        {
            Role = m.Role,
            Content = m.Content,
        }).ToList();

        return Edit(prompt, context);
    }
}
