using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIChatSessionListOptionsDisplayDriver : DisplayDriver<AIChatSessionListOptions>
{
    public override IDisplayResult Edit(AIChatSessionListOptions options, BuildEditorContext context)
    {
        return Initialize<ChatSessionListOptionsViewModel>("ChatSessionSearchOptions_Edit", m =>
        {
            m.SearchText = options.SearchText;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatSessionListOptions options, UpdateEditorContext context)
    {
        var model = new ChatSessionListOptionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        options.SearchText = model.SearchText;

        return Edit(options, context);
    }
}
