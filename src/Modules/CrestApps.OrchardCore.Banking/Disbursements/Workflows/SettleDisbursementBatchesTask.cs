using CrestApps.OrchardCore.Banking.Disbursements.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Records as paid the disbursements of every batch released more than a number of days ago that the bank did not
/// return, such as from a Timer Event once a day after the return window. Writes how many settled to the workflow
/// output <c>SettledDisbursements</c>.
/// </summary>
public sealed class SettleDisbursementBatchesTask : TaskActivity<SettleDisbursementBatchesTask>
{
    /// <summary>
    /// The age a batch settles at when the task does not say: three days, the usual return window for a test entry.
    /// </summary>
    public const int DefaultAgeInDays = 3;

    /// <summary>
    /// The workflow output that receives the number of disbursements that settled.
    /// </summary>
    public const string OutputName = "SettledDisbursements";

    private readonly IDisbursementService _disbursementService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettleDisbursementBatchesTask"/> class.
    /// </summary>
    /// <param name="disbursementService">The disbursement rules.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SettleDisbursementBatchesTask(IDisbursementService disbursementService, IStringLocalizer<SettleDisbursementBatchesTask> stringLocalizer)
    {
        _disbursementService = disbursementService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Settle Disbursement Batches"];

    /// <inheritdoc />
    public override LocalizedString Category => S["Disbursements"];

    /// <summary>
    /// Gets or sets how many days after its release a batch settles.
    /// </summary>
    public int AgeInDays
    {
        get => GetProperty(() => DefaultAgeInDays);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var count = await _disbursementService.SettleReleasedBatchesAsync(Math.Max(1, AgeInDays));

        workflowContext.Output[OutputName] = count;
        workflowContext.LastResult = count;

        return Outcome("Done");
    }
}

/// <summary>
/// Edits the Settle Disbursement Batches task.
/// </summary>
public sealed class SettleDisbursementBatchesTaskDisplayDriver : ActivityDisplayDriver<SettleDisbursementBatchesTask, SettleDisbursementBatchesTaskViewModel>
{
    /// <inheritdoc />
    protected override void EditActivity(SettleDisbursementBatchesTask activity, SettleDisbursementBatchesTaskViewModel model)
    {
        model.AgeInDays = activity.AgeInDays;
    }

    /// <inheritdoc />
    protected override void UpdateActivity(SettleDisbursementBatchesTaskViewModel model, SettleDisbursementBatchesTask activity)
    {
        activity.AgeInDays = Math.Clamp(model.AgeInDays, 1, 60);
    }
}

/// <summary>
/// The fields of the Settle Disbursement Batches task.
/// </summary>
public class SettleDisbursementBatchesTaskViewModel
{
    /// <summary>
    /// Gets or sets how many days after its release a batch settles.
    /// </summary>
    public int AgeInDays { get; set; } = SettleDisbursementBatchesTask.DefaultAgeInDays;
}
