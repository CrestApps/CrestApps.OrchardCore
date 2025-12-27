using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

internal sealed class ChatInteractionToolsDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;

    internal readonly IStringLocalizer S;

    public ChatInteractionToolsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IStringLocalizer<ChatInteractionToolsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        return Initialize<EditChatInteractionToolsViewModel>("ChatInteractionTools_Edit", model =>
        {
            model.Tools = _toolDefinitions.Tools
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"].Value)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = interaction.ToolNames?.Contains(entry.Key) ?? false,
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }).Location("Parameters:5#Tools:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        var model = new EditChatInteractionToolsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

        interaction.ToolNames = selectedToolKeys is null || !selectedToolKeys.Any()
            ? []
            : _toolDefinitions.Tools.Keys
                .Intersect(selectedToolKeys)
                .ToList();

        return Edit(interaction, context);
    }
}
