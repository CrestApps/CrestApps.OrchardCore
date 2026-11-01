using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.WorkOrders.Workflows;

/// <summary>
/// The base of the work order workflow events: optionally limited to some work order types.
/// </summary>
public abstract class WorkOrderEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected WorkOrderEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category => S["Work Orders"];

    /// <summary>
    /// Gets or sets the work order types it starts for; empty for every type.
    /// </summary>
    public IList<string> TypeIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => TypeIds.Count == 0 || (workflowContext.Input.TryGetValue("TypeId", out var typeId) && typeId is string id && TypeIds.Contains(id, StringComparer.Ordinal));

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Starts when a work order is created.
/// </summary>
public sealed class WorkOrderCreatedEvent : WorkOrderEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderCreatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderCreatedEvent(IStringLocalizer<WorkOrderCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => WorkOrdersConstants.Events.Created;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Work Order Created"];
}

/// <summary>
/// Starts when a work order's status changes.
/// </summary>
public sealed class WorkOrderStatusChangedEvent : WorkOrderEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderStatusChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderStatusChangedEvent(IStringLocalizer<WorkOrderStatusChangedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => WorkOrdersConstants.Events.StatusChanged;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Work Order Status Changed"];
}

/// <summary>
/// Starts when a work order is close to missing a target.
/// </summary>
public sealed class WorkOrderSlaWarningEvent : WorkOrderEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSlaWarningEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderSlaWarningEvent(IStringLocalizer<WorkOrderSlaWarningEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => WorkOrdersConstants.Events.SlaWarning;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Work Order Service Level Warning"];
}

/// <summary>
/// Starts when a work order misses a target.
/// </summary>
public sealed class WorkOrderSlaBreachedEvent : WorkOrderEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSlaBreachedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderSlaBreachedEvent(IStringLocalizer<WorkOrderSlaBreachedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => WorkOrdersConstants.Events.SlaBreached;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Work Order Service Level Breached"];
}

/// <summary>
/// Starts when a work order is completed.
/// </summary>
public sealed class WorkOrderCompletedEvent : WorkOrderEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderCompletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderCompletedEvent(IStringLocalizer<WorkOrderCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => WorkOrdersConstants.Events.Completed;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Work Order Completed"];
}

/// <summary>
/// Starts the work order workflow events.
/// </summary>
internal sealed class WorkflowWorkOrderObserver : IWorkOrderObserver
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowWorkOrderObserver"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager.</param>
    public WorkflowWorkOrderObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public Task OnEventAsync(WorkOrder workOrder, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default)
    {
        var input = new Dictionary<string, object>(detail ?? new Dictionary<string, object>())
        {
            ["WorkOrderId"] = workOrder.ItemId,
            ["Number"] = workOrder.Number,
            ["TypeId"] = workOrder.TypeId,
            ["Status"] = workOrder.Status.ToString(),
            ["Priority"] = workOrder.Priority.ToString(),
            ["Summary"] = workOrder.Summary,
            ["CustomerId"] = workOrder.CustomerId,
            ["TerritoryId"] = workOrder.TerritoryId,
        };

        return _workflowManager.TriggerEventAsync(eventName, input, workOrder.ItemId);
    }
}
