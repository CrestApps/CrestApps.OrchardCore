using CrestApps.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Keeps a work order's service level timers in step with the work order, through the shared
/// <see cref="IServiceLevelTracker"/>, and copies their due times to <see cref="WorkOrder.Sla"/>, the read model the
/// work order list, index and reports use.
/// </summary>
public sealed class WorkOrderServiceLevels
{
    /// <summary>
    /// The name of the response target in work-order events.
    /// </summary>
    public const string ResponseTarget = "Response";

    /// <summary>
    /// The name of the resolution target in work-order events.
    /// </summary>
    public const string ResolutionTarget = "Resolution";

    private readonly IServiceLevelTracker _tracker;
    private readonly IServiceLevelPolicyManager _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderServiceLevels"/> class.
    /// </summary>
    /// <param name="tracker">The service level tracker.</param>
    /// <param name="policies">The policy manager.</param>
    public WorkOrderServiceLevels(IServiceLevelTracker tracker, IServiceLevelPolicyManager policies)
    {
        _tracker = tracker;
        _policies = policies;
    }

    /// <summary>
    /// Gets the record a work order's timers belong to.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <returns>The record.</returns>
    public static RecordRef SubjectOf(WorkOrder workOrder)
        => new(WorkOrdersConstants.RecordKind, workOrder.ItemId);

    /// <summary>
    /// Gets the name a metric has in work-order events.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <returns><see cref="ResponseTarget"/>, <see cref="ResolutionTarget"/>, or <see langword="null"/> for another metric.</returns>
    public static string TargetName(string metric)
    {
        if (string.Equals(metric, ServiceLevelMetrics.FirstResponse, StringComparison.OrdinalIgnoreCase))
        {
            return ResponseTarget;
        }

        return string.Equals(metric, ServiceLevelMetrics.Resolution, StringComparison.OrdinalIgnoreCase)
            ? ResolutionTarget
            : null;
    }

    /// <summary>
    /// Starts the response and resolution timers of a new work order, paused when it is created in a status that
    /// pauses them.
    /// </summary>
    /// <param name="workOrder">The work order, with its id, priority and policy.</param>
    /// <param name="nowUtc">When it was created.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the timers are started.</returns>
    public async Task StartAsync(WorkOrder workOrder, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        workOrder.Sla ??= new WorkOrderSla();

        if (!string.IsNullOrEmpty(workOrder.Sla.PolicyId))
        {
            foreach (var metric in new[] { ServiceLevelMetrics.FirstResponse, ServiceLevelMetrics.Resolution })
            {
                await _tracker.StartAsync(new ServiceLevelStart
                {
                    Subject = SubjectOf(workOrder),
                    Metric = metric,
                    PolicyId = workOrder.Sla.PolicyId,
                    Priority = workOrder.Priority.ToString(),
                    StartUtc = nowUtc,
                }, cancellationToken);
            }
        }

        await RefreshAsync(workOrder, cancellationToken);
    }

    /// <summary>
    /// Tells the timers that a work order moved to a status: the first move to a response status answers it, a status
    /// that pauses (in the policy, or a sub-status that pauses) stops the timers and any other starts them again, a
    /// finished work order is resolved and a canceled one stops being measured.
    /// </summary>
    /// <param name="workOrder">The work order, already moved.</param>
    /// <param name="subStatus">Its sub-status, or <see langword="null"/>.</param>
    /// <param name="nowUtc">When it moved.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the timers are told.</returns>
    public async Task MovedAsync(WorkOrder workOrder, WorkOrderSubStatus subStatus, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        var subject = SubjectOf(workOrder);
        var status = workOrder.Status;

        workOrder.Sla ??= new WorkOrderSla();

        if (workOrder.Sla.FirstResponseUtc is null && WorkOrderStatusRules.IsResponse(status))
        {
            workOrder.Sla.FirstResponseUtc = nowUtc;
            await _tracker.CompleteAsync(subject, ServiceLevelMetrics.FirstResponse, nowUtc, cancellationToken);
        }

        if (status == WorkOrderStatus.Canceled)
        {
            await _tracker.CancelAsync(subject, cancellationToken: cancellationToken);
        }
        else if (status is WorkOrderStatus.Completed or WorkOrderStatus.Closed)
        {
            await _tracker.CompleteAsync(subject, ServiceLevelMetrics.FirstResponse, nowUtc, cancellationToken);
            await _tracker.CompleteAsync(subject, ServiceLevelMetrics.Resolution, nowUtc, cancellationToken);
        }
        else if (await PausesAsync(workOrder.Sla.PolicyId, status, subStatus, cancellationToken))
        {
            // A work order reopened into a waiting status counts again once it stops waiting.
            await ReopenAsync(workOrder, subject, cancellationToken);
            await _tracker.PauseAsync(subject, cancellationToken: cancellationToken);
        }
        else
        {
            await ReopenAsync(workOrder, subject, cancellationToken);
            await _tracker.ResumeAsync(subject, cancellationToken: cancellationToken);
        }

        await RefreshAsync(workOrder, cancellationToken);
    }

    /// <summary>
    /// Gives a work order's running timers the target of its priority, after its details were saved. The time already
    /// used is kept.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the timers are retargeted.</returns>
    public async Task UpdatedAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        var priority = workOrder.Priority.ToString();
        var active = await _tracker.GetTimersAsync(SubjectOf(workOrder), activeOnly: true, cancellationToken);

        if (active.Any(timer => !string.Equals(timer.Priority, priority, StringComparison.Ordinal)))
        {
            await _tracker.RetargetAsync(SubjectOf(workOrder), workOrder.Sla?.PolicyId, priority, cancellationToken);
            await RefreshAsync(workOrder, cancellationToken);
        }
    }

    /// <summary>
    /// Copies the due times and the paused state of a work order's timers to <see cref="WorkOrder.Sla"/>. A metric
    /// keeps the due time of its newest timer after it is answered.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the read model is current.</returns>
    public async Task RefreshAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        workOrder.Sla ??= new WorkOrderSla();

        var timers = await _tracker.GetTimersAsync(SubjectOf(workOrder), activeOnly: false, cancellationToken);
        var response = Newest(timers, ServiceLevelMetrics.FirstResponse);
        var resolution = Newest(timers, ServiceLevelMetrics.Resolution);

        // A metric with no timer left to read (none started yet, or only cancelled ones) keeps what the work order shows,
        // so its due time and breach history are never wiped.
        if (response is not null)
        {
            workOrder.Sla.RespondByUtc = response.DueUtc;
            workOrder.Sla.ResponseWarned = response.WarnedUtc is not null || response.IsBreached;
            workOrder.Sla.ResponseBreached = response.IsBreached;
        }

        if (resolution is not null)
        {
            workOrder.Sla.ResolveByUtc = resolution.DueUtc;
            workOrder.Sla.ResolutionWarned = resolution.WarnedUtc is not null || resolution.IsBreached;
            workOrder.Sla.ResolutionBreached = resolution.IsBreached;
        }

        if (response is not null || resolution is not null)
        {
            workOrder.Sla.PausedSinceUtc = timers.Where(timer => timer.State == ServiceLevelTimerState.Paused).Select(timer => timer.PausedSinceUtc).Min();
        }
    }

    // A work order that leaves Completed or Closed counts its resolution again from the time it had used.
    private async Task ReopenAsync(WorkOrder workOrder, RecordRef subject, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(workOrder.Sla?.PolicyId))
        {
            await _tracker.ReopenAsync(subject, ServiceLevelMetrics.Resolution, cancellationToken);
        }
    }

    private static ServiceLevelTimer Newest(IEnumerable<ServiceLevelTimer> timers, string metric)
        => timers
            .Where(timer => timer.State != ServiceLevelTimerState.Cancelled && string.Equals(timer.Metric, metric, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(timer => timer.StartedUtc)
            .FirstOrDefault();

    private async Task<bool> PausesAsync(string policyId, WorkOrderStatus status, WorkOrderSubStatus subStatus, CancellationToken cancellationToken)
    {
        if (subStatus?.PausesSla == true)
        {
            return true;
        }

        if (string.IsNullOrEmpty(policyId) || await _policies.FindByIdAsync(policyId, cancellationToken) is not { } policy)
        {
            return false;
        }

        return policy.TryGet<WorkOrderServiceLevelSettings>(out var settings) && settings.PauseStatuses.Contains(status);
    }
}
