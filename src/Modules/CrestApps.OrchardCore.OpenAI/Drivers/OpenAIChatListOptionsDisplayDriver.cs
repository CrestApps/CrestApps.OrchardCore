using CrestApps.OrchardCore.OpenAI.ViewModels;
using CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public class OpenAIChatListOptionsDisplayDriver : DisplayDriver<OpenAIChatListOptions>
{
    public override IDisplayResult Edit(OpenAIChatListOptions model, BuildEditorContext context)
    {
        return Initialize<ChatListOptionsViewModel>("OpenAIChatListOptionsSearch_Edit", m =>
        {
            m.SearchText = model.SearchText;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(OpenAIChatListOptions model, UpdateEditorContext context)
    {
        var viewModel = new ChatListOptionsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        model.SearchText = viewModel.SearchText;

        return Edit(model, context);
    }
}
