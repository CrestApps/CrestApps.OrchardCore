using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Banking.Workflows;

/// <summary>
/// Starts when a bank account's status changes, such as when it is verified or closed; it can start for one status only.
/// </summary>
public sealed class BankAccountStatusChangedEvent : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountStatusChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankAccountStatusChangedEvent(IStringLocalizer<BankAccountStatusChangedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc />
    public override string Name => nameof(BankAccountStatusChangedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Bank Account Status Changed"];

    /// <inheritdoc />
    public override LocalizedString Category => S["Banking"];

    /// <summary>
    /// Gets or sets the new status the event starts for, such as Verified or Closed; empty for every status.
    /// </summary>
    public string Status
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => string.IsNullOrWhiteSpace(Status)
            || (workflowContext.Input.TryGetValue("Status", out var status) && string.Equals(status as string, Status.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Edits the Bank Account Status Changed event: the status it starts for.
/// </summary>
public sealed class BankAccountStatusChangedEventDisplayDriver : ActivityDisplayDriver<BankAccountStatusChangedEvent, BankAccountStatusChangedEventViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountStatusChangedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankAccountStatusChangedEventDisplayDriver(IStringLocalizer<BankAccountStatusChangedEventDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(BankAccountStatusChangedEvent activity, BuildEditorContext context)
        => Initialize<BankAccountStatusChangedEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));

    /// <inheritdoc />
    protected override void EditActivity(BankAccountStatusChangedEvent activity, BankAccountStatusChangedEventViewModel model)
        => model.Status = activity.Status;

    /// <inheritdoc />
    protected override void UpdateActivity(BankAccountStatusChangedEventViewModel model, BankAccountStatusChangedEvent activity)
        => activity.Status = string.IsNullOrWhiteSpace(model.Status) ? null : model.Status.Trim();
}

/// <summary>
/// The editor of the Bank Account Status Changed event.
/// </summary>
public class BankAccountStatusChangedEventViewModel
{
    /// <summary>
    /// Gets or sets the new status, or empty for every status.
    /// </summary>
    public string Status { get; set; }
}
