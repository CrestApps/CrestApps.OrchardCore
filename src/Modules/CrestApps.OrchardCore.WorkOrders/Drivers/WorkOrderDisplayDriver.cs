using System.Reflection;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
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

    private static readonly PropertyInfo[] _editProperties = [.. typeof(WorkOrderEditViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanRead && property.CanWrite)];

    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderLookups _lookups;
    private readonly WorkOrderCustomers _customers;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public WorkOrderDisplayDriver(
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderLookups lookups,
        WorkOrderCustomers customers,
        ILocalClock localClock,
        IClock clock,
        IServiceProvider serviceProvider,
        IStringLocalizer<WorkOrderDisplayDriver> stringLocalizer)
    {
        _types = types;
        _lookups = lookups;
        _customers = customers;
        _localClock = localClock;
        _clock = clock;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WorkOrder workOrder, BuildEditorContext context)
    {
        // One shape per card, each a copy of one populated view model: the lookups run once.
        Task<WorkOrderEditViewModel> populated = null;

        async ValueTask CopyAsync(WorkOrderEditViewModel model)
        {
            populated ??= PopulateAsync(workOrder);

            Copy(await populated, model);
        }

        return Combine(
            Initialize<WorkOrderEditViewModel>("WorkOrderFields_Edit", CopyAsync)
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<WorkOrderEditViewModel>("WorkOrderWho_Edit", CopyAsync)
                .Location(l => l.Zone("Content", "1").Card(S["Who"], "20")),
            Initialize<WorkOrderEditViewModel>("WorkOrderWhere_Edit", CopyAsync)
                .Location(l => l.Zone("Content", "1").Card(S["Where"], "30")),
            Initialize<WorkOrderEditViewModel>("WorkOrderWhen_Edit", CopyAsync)
                .Location(l => l.Zone("Content", "1").Card(S["When"], "40")),
            Initialize<WorkOrderEditViewModel>("WorkOrderAssets_Edit", CopyAsync)
                .Location(l => l.Zone("Content", "1").Card(S["Assets"], "50")),
            Initialize<WorkOrderEditViewModel>("WorkOrderLines_Edit", CopyAsync)
                .Location(l => l.Zone("Content", "1").Card(S["Lines"], "60")));
    }

    private async Task<WorkOrderEditViewModel> PopulateAsync(WorkOrder workOrder)
    {
        var model = new WorkOrderEditViewModel();

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

        return model;
    }

    private static void Copy(WorkOrderEditViewModel source, WorkOrderEditViewModel target)
    {
        foreach (var property in _editProperties)
        {
            property.SetValue(target, property.GetValue(source));
        }
    }

    public override async Task<IDisplayResult> UpdateAsync(WorkOrder workOrder, UpdateEditorContext context)
    {
        var model = new WorkOrderEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        await WorkOrderEditorRules.ApplyAsync(workOrder, model, _customers, _localClock, _clock);

        return Edit(workOrder, context);
    }
}
