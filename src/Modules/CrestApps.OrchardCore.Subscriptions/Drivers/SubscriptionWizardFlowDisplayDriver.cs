using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Wizard;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

internal sealed class SubscriptionWizardFlowDisplayDriver : DisplayDriver<WizardFlow>
{
    private readonly IDisplayManager<SubscriptionFlow> _subscriptionFlowDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly SubscriptionWizardFlowFactory _flowFactory;

    public SubscriptionWizardFlowDisplayDriver(
        IDisplayManager<SubscriptionFlow> subscriptionFlowDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        SubscriptionWizardFlowFactory flowFactory)
    {
        _subscriptionFlowDisplayManager = subscriptionFlowDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _flowFactory = flowFactory;
    }

    public override async Task<IDisplayResult> DisplayAsync(WizardFlow model, BuildDisplayContext context)
    {
        var flowContext = await _flowFactory.CreateAsync(model);

        if (flowContext == null)
        {
            return null;
        }

        var shape = await _subscriptionFlowDisplayManager.BuildDisplayAsync(
            flowContext.Flow,
            _updateModelAccessor.ModelUpdater,
            context.DisplayType);

        flowContext.SyncToWizardSession();

        return CreateZoneResults(shape);
    }

    public override async Task<IDisplayResult> EditAsync(WizardFlow model, BuildEditorContext context)
    {
        var flowContext = await _flowFactory.CreateAsync(model);

        if (flowContext == null)
        {
            return null;
        }

        var shape = await _subscriptionFlowDisplayManager.BuildEditorAsync(flowContext.Flow, context.Updater, false);

        flowContext.SyncToWizardSession();

        return CreateZoneResults(shape);
    }

    public override async Task<IDisplayResult> UpdateAsync(WizardFlow model, UpdateEditorContext context)
    {
        var flowContext = await _flowFactory.CreateAsync(model);

        if (flowContext == null)
        {
            return null;
        }

        var shape = await _subscriptionFlowDisplayManager.UpdateEditorAsync(flowContext.Flow, context.Updater, false);

        flowContext.SyncToWizardSession();

        return CreateZoneResults(shape);
    }

    private CombinedResult CreateZoneResults(object shape)
    {
        var results = new List<IDisplayResult>();

        AddZoneResult(results, "Steps", GetZone(shape, "Steps"));
        AddZoneResult(results, "Header", GetZone(shape, "Header"));
        AddZoneResult(results, "Content", GetZone(shape, "Content"));
        AddZoneResult(results, "Actions", GetZone(shape, "Actions"));
        AddZoneResult(results, "Footer", GetZone(shape, "Footer"));

        return results.Count == 0 ? null : Combine(results.ToArray());
    }

    private void AddZoneResult(
        List<IDisplayResult> results,
        string zoneName,
        IShape zone)
    {
        _ = _flowFactory;

        if (zone == null)
        {
            return;
        }

        results.Add(Factory($"SubscriptionWizard{zoneName}", async _ => zone).Location(zoneName));
    }

    private static IShape GetZone(object shape, string zoneName)
        => shape?.GetType().GetProperty(zoneName)?.GetValue(shape) as IShape;
}
