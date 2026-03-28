using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class ChatInteractionListOptionsDisplayDriver : DisplayDriver<ChatInteractionListOptions>
{
    public override IDisplayResult Edit(ChatInteractionListOptions options, BuildEditorContext context)
    {
        return Initialize<ChatInteractionListOptions>("ChatInteractionSearchOptions_Edit", m =>
        {
            m.SearchText = options.SearchText;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteractionListOptions options, UpdateEditorContext context)
    {
        var model = new ChatInteractionListOptions();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        options.SearchText = model.SearchText;

        return Edit(options, context);
    }
}
