using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.WorkOrders.Drivers;

/// <summary>
/// The main section of the work order editor: details, who and where, when, the assets and the planned lines. Other
/// modules add their own sections to the editor with display drivers of <see cref="WorkOrder"/>.
/// </summary>
internal sealed class WorkOrderDisplayDriver : DisplayDriver<WorkOrder>
{
    private const int BlankLines = 2;

    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderLookups _lookups;
    private readonly WorkOrderCustomers _customers;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;

    public WorkOrderDisplayDriver(
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderLookups lookups,
        WorkOrderCustomers customers,
        ILocalClock localClock,
        IClock clock,
        IServiceProvider serviceProvider)
    {
        _types = types;
        _lookups = lookups;
        _customers = customers;
        _localClock = localClock;
        _clock = clock;
        _serviceProvider = serviceProvider;
    }

    public override IDisplayResult Edit(WorkOrder workOrder, BuildEditorContext context)
    {
        return Initialize<WorkOrderEditViewModel>("WorkOrderFields_Edit", async model =>
        {
            await WorkOrderEditorRules.FillAsync(workOrder, model, _localClock);

            model.TypeName = string.IsNullOrEmpty(workOrder.TypeId) ? null : (await _types.FindByIdAsync(workOrder.TypeId))?.Name;
            model.AssetOptions = [.. (await _lookups.AssetsAsync()).Select(option => new SelectListItem(option.Text, option.Value, model.AssetIds.Contains(option.Value)))];
            model.StockItemOptions = await _lookups.StockItemsAsync();
            model.CustomerPickers = await _customers.NamesAsync(model.CustomerId, model.AccountId, model.ContactId, pickers: true);
            model.Map = _serviceProvider.GetService<IGeoSettingsService>() is { } geo ? await geo.GetAsync() : null;

            while (model.Lines.Count(line => string.IsNullOrWhiteSpace(line.Description)) < BlankLines)
            {
                model.Lines.Add(new LineEditModel());
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(WorkOrder workOrder, UpdateEditorContext context)
    {
        var model = new WorkOrderEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        await WorkOrderEditorRules.ApplyAsync(workOrder, model, _customers, _localClock, _clock);

        return Edit(workOrder, context);
    }
}
