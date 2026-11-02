using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Dispatch.Workflows;

/// <summary>
/// Starts when a visit is assigned, reassigned or moved.
/// </summary>
public sealed class VisitAssignedEvent : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VisitAssignedEvent"/> class.
    /// </summary>
    public VisitAssignedEvent(IStringLocalizer<VisitAssignedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override string Name => DispatchConstants.Events.Assigned;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Visit Assigned"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Dispatch"];

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Starts when a visit's status changes, to the statuses chosen or any.
/// </summary>
public sealed class VisitStatusChangedEvent : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VisitStatusChangedEvent"/> class.
    /// </summary>
    public VisitStatusChangedEvent(IStringLocalizer<VisitStatusChangedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override string Name => DispatchConstants.Events.StatusChanged;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Visit Status Changed"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Dispatch"];

    /// <summary>
    /// Gets or sets the statuses it starts for; every status when empty.
    /// </summary>
    public IList<string> Statuses
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Statuses.Count == 0 || (workflowContext.Input.TryGetValue("Status", out var status) && status is string value && Statuses.Contains(value, StringComparer.Ordinal));

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Starts when a visit becomes at risk: running over, not started, arriving late, or its technician off.
/// </summary>
public sealed class VisitAtRiskEvent : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VisitAtRiskEvent"/> class.
    /// </summary>
    public VisitAtRiskEvent(IStringLocalizer<VisitAtRiskEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override string Name => DispatchConstants.Events.AtRisk;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Visit At Risk"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Dispatch"];

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// The display of the visit at risk event, which has no settings.
/// </summary>
public sealed class VisitAtRiskEventDisplayDriver : ActivityDisplayDriver<VisitAtRiskEvent>
{
}

/// <summary>
/// The display of the visit assigned event, which has no settings.
/// </summary>
public sealed class VisitAssignedEventDisplayDriver : ActivityDisplayDriver<VisitAssignedEvent>
{
}

/// <summary>
/// The display and settings of the visit status changed event.
/// </summary>
public sealed class VisitStatusChangedEventDisplayDriver : ActivityDisplayDriver<VisitStatusChangedEvent, VisitStatusChangedEventViewModel>
{
    /// <inheritdoc/>
    protected override ValueTask EditActivityAsync(VisitStatusChangedEvent activity, VisitStatusChangedEventViewModel model)
    {
        model.Statuses = [.. activity.Statuses];
        model.StatusOptions = [.. Enum.GetValues<VisitStatus>().Where(status => status is not (VisitStatus.Unscheduled or VisitStatus.Scheduled)).Select(status => new SelectListItem(status.Text(), status.ToString(), model.Statuses.Contains(status.ToString())))];

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(VisitStatusChangedEvent activity, UpdateEditorContext context)
    {
        var model = new VisitStatusChangedEventViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        activity.Statuses = [.. (model.Statuses ?? []).Where(status => Enum.TryParse<VisitStatus>(status, out _)).Distinct(StringComparer.Ordinal)];

        return Edit(activity, context);
    }
}

/// <summary>
/// The settings of the visit status changed event.
/// </summary>
public class VisitStatusChangedEventViewModel
{
    /// <summary>
    /// Gets or sets the statuses it starts for.
    /// </summary>
    public IList<string> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];
}

/// <summary>
/// Starts the visit workflow events.
/// </summary>
internal sealed class WorkflowVisitObserver : IVisitObserver
{
    private readonly IWorkflowManager _workflowManager;

    public WorkflowVisitObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public Task OnEventAsync(ServiceVisit visit, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default)
    {
        if (eventName is not (DispatchConstants.Events.Assigned or DispatchConstants.Events.StatusChanged or DispatchConstants.Events.AtRisk))
        {
            return Task.CompletedTask;
        }

        var input = new Dictionary<string, object>(detail ?? new Dictionary<string, object>())
        {
            ["VisitId"] = visit.ItemId,
            ["Number"] = visit.Number,
            ["WorkOrderId"] = visit.WorkOrderId,
            ["WorkOrderNumber"] = visit.WorkOrderNumber,
            ["Status"] = visit.Status.ToString(),
            ["LeadWorkerId"] = visit.LeadWorkerId,
            ["WorkerIds"] = visit.WorkerIds.ToArray(),
            ["StartUtc"] = visit.StartUtc,
            ["EndUtc"] = visit.EndUtc,
            ["ArrivalStartUtc"] = visit.ArrivalStartUtc,
            ["ArrivalEndUtc"] = visit.ArrivalEndUtc,
        };

        return _workflowManager.TriggerEventAsync(eventName, input, visit.ItemId);
    }
}
