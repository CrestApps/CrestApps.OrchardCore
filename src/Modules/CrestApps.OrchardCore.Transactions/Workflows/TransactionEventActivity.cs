using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// A workflow event about a transaction, which can be limited to the transactions of some sources (such as a payment
/// provider) and of some reference types (such as an invoice or an order).
/// </summary>
public abstract class TransactionEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected TransactionEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Transactions"];

    /// <summary>
    /// Gets or sets the sources whose transactions start the workflow, comma separated; empty for every source.
    /// </summary>
    public string Sources
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the reference types whose transactions start the workflow, comma separated; empty for every type.
    /// </summary>
    public string ReferenceTypes
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"])];
    }

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var input = workflowContext.Input;

        return Matches(Sources, Read(input, TransactionWorkflowInput.Source)) &&
            Matches(ReferenceTypes, Read(input, TransactionWorkflowInput.ReferenceType));
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }

    private static bool Matches(string filter, string value)
    {
        var allowed = (filter ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return allowed.Length == 0 || allowed.Contains(value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static string Read(IDictionary<string, object> input, string key)
    {
        return input is not null && input.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
