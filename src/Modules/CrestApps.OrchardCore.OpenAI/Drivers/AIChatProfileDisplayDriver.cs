using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class AIChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    public override IDisplayResult Edit(AIChatProfile model, BuildEditorContext context)
    {
        return Initialize<EditAIChatProfileViewModel>("AIChatProfile_Edit", m =>
        {
            m.Title = model.Title;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile model, UpdateEditorContext context)
    {
        var viewModel = new EditAIChatProfileViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        model.Title = viewModel.Title;

        return Edit(model, context);
    }
}
