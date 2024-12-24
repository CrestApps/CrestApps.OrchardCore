using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class AIChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    public override Task<IDisplayResult> DisplayAsync(AIChatProfile model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIChatProfile_Fields_SummaryAdmin", model).Location("Content:1"),
            View("AIChatProfile_Buttons_SummaryAdmin", model).Location("Actions:5"),
            View("AIChatProfile_DefaultTags_SummaryAdmin", model).Location("Tags:5"),
            View("AIChatProfile_DefaultMeta_SummaryAdmin", model).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIChatProfile model, BuildEditorContext context)
    {
        return Initialize<EditAIChatProfileViewModel>("AIChatProfileTitle_Edit", m =>
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
