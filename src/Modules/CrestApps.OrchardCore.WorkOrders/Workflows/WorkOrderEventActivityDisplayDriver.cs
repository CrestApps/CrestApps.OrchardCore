using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.WorkOrders.Workflows;

/// <summary>
/// Edits the work order types a work order event starts for.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class WorkOrderEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, WorkOrderEventActivityViewModel>
    where TActivity : WorkOrderEventActivity
{
    private readonly WorkOrderSettingManager<WorkOrderType> _types;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="types">The work order types.</param>
    public WorkOrderEventActivityDisplayDriver(WorkOrderSettingManager<WorkOrderType> types)
    {
        _types = types;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(TActivity activity, WorkOrderEventActivityViewModel model)
    {
        model.TypeIds = [.. activity.TypeIds];
        model.TypeOptions = [.. (await _types.GetAllAsync()).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).Select(type => new SelectListItem(type.Name, type.ItemId, model.TypeIds.Contains(type.ItemId)))];
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TActivity activity, UpdateEditorContext context)
    {
        var model = new WorkOrderEventActivityViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        activity.TypeIds = [.. (model.TypeIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)];

        return Edit(activity, context);
    }
}

/// <summary>
/// The work order event editor.
/// </summary>
public class WorkOrderEventActivityViewModel
{
    /// <summary>
    /// Gets or sets the work order types it starts for.
    /// </summary>
    public IList<string> TypeIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the work order types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];
}
