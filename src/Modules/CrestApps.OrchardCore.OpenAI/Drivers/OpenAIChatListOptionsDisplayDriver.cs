using CrestApps.OrchardCore.OpenAI.ViewModels;
using CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class OpenAIChatListOptionsDisplayDriver : DisplayDriver<OpenAIChatListOptions>
{
    public override IDisplayResult Edit(OpenAIChatListOptions options, BuildEditorContext context)
    {
        return Initialize<ChatListOptionsViewModel>("OpenAIChatListOptionsSearch_Edit", m =>
        {
            m.SearchText = options.SearchText;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(OpenAIChatListOptions options, UpdateEditorContext context)
    {
        var model = new ChatListOptionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        options.SearchText = model.SearchText;

        return Edit(options, context);
    }
}
