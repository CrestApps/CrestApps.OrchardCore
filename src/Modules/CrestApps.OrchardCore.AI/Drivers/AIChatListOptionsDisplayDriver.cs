using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.ViewModels.Sessions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class AIChatListOptionsDisplayDriver : DisplayDriver<AIChatListOptions>
{
    public override IDisplayResult Edit(AIChatListOptions options, BuildEditorContext context)
    {
        return Initialize<ChatListOptionsViewModel>("AIChatListOptionsSearch_Edit", m =>
        {
            m.SearchText = options.SearchText;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatListOptions options, UpdateEditorContext context)
    {
        var model = new ChatListOptionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        options.SearchText = model.SearchText;

        return Edit(options, context);
    }
}
