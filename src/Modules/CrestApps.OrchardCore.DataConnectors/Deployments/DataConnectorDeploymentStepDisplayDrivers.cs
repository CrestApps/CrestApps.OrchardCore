using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DataConnectors.Deployments;

internal sealed class DataConnectionDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, DataConnectionDeploymentStep>
{
    private readonly IDataConnectionStore _connectionStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectionDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="connectionStore">The connection store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataConnectionDeploymentStepDisplayDriver(
        IDataConnectionStore connectionStore,
        IStringLocalizer<DataConnectionDeploymentStepDisplayDriver> stringLocalizer)
    {
        _connectionStore = connectionStore;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(DataConnectionDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("DataConnectionDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("DataConnectionDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(DataConnectionDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<DataConnectorDeploymentStepViewModel>("DataConnectionDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Entries =
            [
                .. (await _connectionStore.ListAsync()).Select(connection => new DataConnectorDeploymentEntryViewModel
                {
                    ItemId = connection.ItemId,
                    DisplayText = connection.DisplayText,
                    IsSelected = step.ItemIds?.Contains(connection.ItemId) == true,
                }),
            ];
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(DataConnectionDeploymentStep step, UpdateEditorContext context)
    {
        var model = new DataConnectorDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.IncludeAll, m => m.Entries);

        step.IncludeAll = model.IncludeAll;
        step.ItemIds = model.IncludeAll ? [] : [.. (model.Entries ?? []).Where(entry => entry.IsSelected).Select(entry => entry.ItemId)];

        if (!step.IncludeAll && step.ItemIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Entries), S["Select at least one connection, or include them all."]);
        }

        return Edit(step, context);
    }
}

internal sealed class ConnectedDataSetDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ConnectedDataSetDeploymentStep>
{
    private readonly IConnectedDataSetStore _dataSetStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectedDataSetDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataSetStore">The data set store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConnectedDataSetDeploymentStepDisplayDriver(
        IConnectedDataSetStore dataSetStore,
        IStringLocalizer<ConnectedDataSetDeploymentStepDisplayDriver> stringLocalizer)
    {
        _dataSetStore = dataSetStore;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ConnectedDataSetDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ConnectedDataSetDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ConnectedDataSetDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(ConnectedDataSetDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<DataConnectorDeploymentStepViewModel>("ConnectedDataSetDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Entries =
            [
                .. (await _dataSetStore.ListAsync()).Select(dataSet => new DataConnectorDeploymentEntryViewModel
                {
                    ItemId = dataSet.ItemId,
                    DisplayText = dataSet.DisplayText,
                    IsSelected = step.ItemIds?.Contains(dataSet.ItemId) == true,
                }),
            ];
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ConnectedDataSetDeploymentStep step, UpdateEditorContext context)
    {
        var model = new DataConnectorDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.IncludeAll, m => m.Entries);

        step.IncludeAll = model.IncludeAll;
        step.ItemIds = model.IncludeAll ? [] : [.. (model.Entries ?? []).Where(entry => entry.IsSelected).Select(entry => entry.ItemId)];

        if (!step.IncludeAll && step.ItemIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Entries), S["Select at least one data set, or include them all."]);
        }

        return Edit(step, context);
    }
}
