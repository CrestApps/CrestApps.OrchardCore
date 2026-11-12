using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Staff.Workflows;

/// <summary>
/// The names of the Staff workflow events.
/// </summary>
public static class StaffWorkflowEventNames
{
    /// <summary>
    /// A person started working: hired, rehired or back from suspension.
    /// </summary>
    public const string Hired = "StaffMemberHired";

    /// <summary>
    /// A person left the business.
    /// </summary>
    public const string Left = "StaffMemberLeft";

    /// <summary>
    /// A person got a new manager.
    /// </summary>
    public const string ManagerChanged = "StaffManagerChanged";
}

/// <summary>
/// The base of the Staff workflow events.
/// </summary>
public abstract class StaffEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaffEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected StaffEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Staff"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Starts when a person starts working.
/// </summary>
public sealed class StaffMemberHiredEvent : StaffEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaffMemberHiredEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StaffMemberHiredEvent(IStringLocalizer<StaffMemberHiredEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => StaffWorkflowEventNames.Hired;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Staff Member Hired"];
}

/// <summary>
/// Starts when a person leaves the business.
/// </summary>
public sealed class StaffMemberLeftEvent : StaffEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaffMemberLeftEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StaffMemberLeftEvent(IStringLocalizer<StaffMemberLeftEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => StaffWorkflowEventNames.Left;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Staff Member Left"];
}

/// <summary>
/// Starts when a person gets a new manager.
/// </summary>
public sealed class StaffManagerChangedEvent : StaffEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaffManagerChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StaffManagerChangedEvent(IStringLocalizer<StaffManagerChangedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => StaffWorkflowEventNames.ManagerChanged;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Staff Manager Changed"];
}

/// <summary>
/// Shows the Staff workflow events in the workflow editor; they have no settings.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class StaffEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : StaffEventActivity
{
}

/// <summary>
/// Starts the Staff workflow events when people start working, leave or get a new manager.
/// </summary>
internal sealed class StaffWorkflowLifecycleObserver : IStaffLifecycleObserver
{
    private readonly IWorkflowManager _workflowManager;

    public StaffWorkflowLifecycleObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public async Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
    {
        var staff = context.StaffMember;
        var input = new Dictionary<string, object>
        {
            ["StaffMemberId"] = staff.ItemId,
            ["UserId"] = staff.UserId,
            ["DisplayName"] = staff.DisplayName,
            ["JobTitle"] = staff.JobTitle,
            ["ManagerId"] = staff.ManagerId,
            ["PreviousManagerId"] = context.PreviousManagerId,
            ["TeamId"] = staff.PrimaryTeamId,
            ["Status"] = staff.Status.ToString(),
        };

        if (context.Activated)
        {
            await _workflowManager.TriggerEventAsync(StaffWorkflowEventNames.Hired, input, staff.ItemId);
        }

        if (context.Terminated)
        {
            await _workflowManager.TriggerEventAsync(StaffWorkflowEventNames.Left, input, staff.ItemId);
        }

        if (context.ManagerChanged)
        {
            await _workflowManager.TriggerEventAsync(StaffWorkflowEventNames.ManagerChanged, input, staff.ItemId);
        }
    }
}
