using CrestApps.Core;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.WorkOrders.Agreements;

/// <summary>
/// Starts when a service agreement is close to its end date, such as to offer a renewal.
/// </summary>
public sealed class ServiceAgreementExpiringEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementExpiringEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ServiceAgreementExpiringEvent(IStringLocalizer<ServiceAgreementExpiringEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => WorkOrdersConstants.Events.AgreementExpiring;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Service Agreement Expiring"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Work Orders"];

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
/// Displays the expiring event in the workflow editor.
/// </summary>
public sealed class ServiceAgreementExpiringEventDisplayDriver : ActivityDisplayDriver<ServiceAgreementExpiringEvent>
{
}

/// <summary>
/// Starts the expiring event of an agreement with visits.
/// </summary>
internal sealed class WorkflowServiceAgreementObserver : IAgreementObserver
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowServiceAgreementObserver"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager.</param>
    public WorkflowServiceAgreementObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public Task OnExpiringAsync(Agreement agreement, CancellationToken cancellationToken = default)
        => !agreement.TryGet<WorkOrderAgreementTerms>(out var terms) || terms is null
            ? Task.CompletedTask
            : _workflowManager.TriggerEventAsync(WorkOrdersConstants.Events.AgreementExpiring, new Dictionary<string, object>
            {
                ["AgreementId"] = agreement.ItemId,
                ["Number"] = agreement.Number,
                ["Name"] = agreement.Name,
                ["EndDate"] = agreement.EndDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ["CustomerId"] = terms.CustomerId,
                ["AccountId"] = agreement.AccountId,
            }, agreement.ItemId);

    /// <inheritdoc/>
    public Task OnHoursUsedAsync(Agreement agreement, int percent, AgreementBalance balance, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
