using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Tools.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class AIToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    internal readonly IStringLocalizer S;

    public AIToolInstanceDisplayDriver(
        IStringLocalizer<AIToolInstanceDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIToolInstance instance, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIToolInstance_Fields_SummaryAdmin", instance).Location("Content:1"),
            View("AIToolInstance_Buttons_SummaryAdmin", instance).Location("Actions:5"),
            View("AIToolInstance_DefaultTags_SummaryAdmin", instance).Location("Tags:5"),
            View("AIToolInstance_DefaultMeta_SummaryAdmin", instance).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        return Initialize<AIToolInstanceFieldsViewModel>("AIToolInstanceFields_Edit", model =>
        {
            model.DisplayText = instance.DisplayText;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        var model = new AIToolInstanceFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(nameof(model.DisplayText), S["The Title field is required."]);
        }

        instance.DisplayText = model.DisplayText;

        return Edit(instance, context);
    }
}
