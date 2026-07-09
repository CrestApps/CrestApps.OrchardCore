using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIDataSourceExternalFieldsDisplayDriver : DisplayDriver<AIDataSource>
{
    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (string.Equals(
            AIDataSourceDriverHelper.GetSourceType(dataSource),
            AIDataSourceSourceTypes.SearchIndexProfile,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditAIDataSourceExternalFieldsViewModel>("AIDataSourceExternalFields_Edit", model =>
        {
            model.KeyFieldName = dataSource.KeyFieldName;
            model.TitleFieldName = dataSource.TitleFieldName;
            model.ContentFieldName = dataSource.ContentFieldName;
            model.IsConfigurationLocked = AIDataSourceDriverHelper.IsConfigurationLocked(dataSource);
        }).Location("Content:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (string.Equals(
            AIDataSourceDriverHelper.GetSourceType(dataSource),
            AIDataSourceSourceTypes.SearchIndexProfile,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new EditAIDataSourceExternalFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!AIDataSourceDriverHelper.IsConfigurationLocked(dataSource))
        {
            dataSource.KeyFieldName = model.KeyFieldName;
            dataSource.TitleFieldName = model.TitleFieldName;
            dataSource.ContentFieldName = model.ContentFieldName;
        }

        return Edit(dataSource, context);
    }
}
