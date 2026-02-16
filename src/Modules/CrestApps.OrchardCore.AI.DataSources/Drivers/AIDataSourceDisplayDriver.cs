using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    internal readonly IStringLocalizer S;

    public AIDataSourceDisplayDriver(IStringLocalizer<AIDataSourceDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIDataSource dataSource, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIDataSource_Fields_SummaryAdmin", dataSource).Location("Content:1"),
            View("AIDataSource_Buttons_SummaryAdmin", dataSource).Location("Actions:5"),
            View("AIDataSource_DefaultTags_SummaryAdmin", dataSource).Location("Tags:5"),
            View("AIDataSource_DefaultMeta_SummaryAdmin", dataSource).Location("Meta:5"),
            View("AIDataSource_ActionsMenu_SummaryAdmin", dataSource).Location("ActionsMenu:10")
        );
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        return Initialize<EditAIDataSourceViewModel>("AIDataSourceFields_Edit", model =>
        {
            model.DisplayText = dataSource.DisplayText;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        var model = new EditAIDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The name is required field."]);
        }

        dataSource.DisplayText = model.DisplayText;

        return Edit(dataSource, context);
    }
}
