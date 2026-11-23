using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Unsubscribes an address from a mailing list, or from every list when no list is given. Every value supports Liquid.
/// </summary>
public sealed class UnsubscribeFromMailingListTask : TaskActivity<UnsubscribeFromMailingListTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberService _members;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsubscribeFromMailingListTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="lists">The list manager.</param>
    /// <param name="members">The member service.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public UnsubscribeFromMailingListTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IMailingListManager lists,
        IMailingListMemberService members,
        IStringLocalizer<UnsubscribeFromMailingListTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _lists = lists;
        _members = members;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category
        => S["Mailing Lists"];

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Unsubscribe from Mailing List"];

    /// <summary>
    /// Gets or sets the list's id or technical name; empty for every list.
    /// </summary>
    public string List
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public string Email
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var listValue = await WorkflowValues.EvaluateAsync(_expressionEvaluator, List, workflowContext);
        string listId = null;

        if (!string.IsNullOrEmpty(listValue))
        {
            var list = await WorkflowValues.FindListAsync(_lists, listValue);

            if (list is null)
            {
                workflowContext.LastResult = "The mailing list was not found.";

                return Outcome("Failed");
            }

            listId = list.ItemId;
        }

        var email = await WorkflowValues.EvaluateAsync(_expressionEvaluator, Email, workflowContext);

        if (string.IsNullOrEmpty(email))
        {
            workflowContext.LastResult = "No e-mail address was given.";

            return Outcome("Failed");
        }

        workflowContext.LastResult = await _members.UnsubscribeAsync(listId, email, MailingListMemberSources.Workflow);

        return Outcome("Done");
    }
}
