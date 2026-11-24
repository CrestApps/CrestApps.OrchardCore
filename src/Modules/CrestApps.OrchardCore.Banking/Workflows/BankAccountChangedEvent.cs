using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Banking.Workflows;

/// <summary>
/// Starts when a bank account is added, its number or status changes, or it is removed.
/// </summary>
public sealed class BankAccountChangedEvent : EventActivity
{
    /// <summary>
    /// The event's name.
    /// </summary>
    public const string EventName = "BankAccountChanged";

    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankAccountChangedEvent(IStringLocalizer<BankAccountChangedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc />
    public override string Name => EventName;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Bank Account Changed"];

    /// <inheritdoc />
    public override LocalizedString Category => S["Banking"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}
