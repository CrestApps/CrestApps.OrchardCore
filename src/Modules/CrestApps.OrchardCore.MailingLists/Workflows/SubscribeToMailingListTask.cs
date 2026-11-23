using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Signs an address up to a mailing list, as the list says (a confirmation e-mail on a double opt-in list), so any form
/// can sign people up. The list is named by its id or technical name; every value supports Liquid.
/// </summary>
public sealed class SubscribeToMailingListTask : TaskActivity<SubscribeToMailingListTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberService _members;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscribeToMailingListTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="lists">The list manager.</param>
    /// <param name="members">The member service.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SubscribeToMailingListTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IMailingListManager lists,
        IMailingListMemberService members,
        IStringLocalizer<SubscribeToMailingListTask> stringLocalizer)
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
        => S["Subscribe to Mailing List"];

    /// <summary>
    /// Gets or sets the list's id or technical name.
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

    /// <summary>
    /// Gets or sets the person's name.
    /// </summary>
    public string PersonName
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
        var list = await WorkflowValues.FindListAsync(_lists, listValue);

        if (list is null)
        {
            workflowContext.LastResult = "The mailing list was not found.";

            return Outcome("Failed");
        }

        var result = await _members.SubscribeAsync(new MailingListSubscribeRequest
        {
            ListId = list.ItemId,
            Email = await WorkflowValues.EvaluateAsync(_expressionEvaluator, Email, workflowContext),
            Name = await WorkflowValues.EvaluateAsync(_expressionEvaluator, PersonName, workflowContext),
            Source = MailingListMemberSources.Workflow,
            SourceId = workflowContext.WorkflowId,
            ConsentForm = workflowContext.WorkflowType?.Name,
        });

        workflowContext.LastResult = result.Succeeded ? result.Member?.ItemId : result.Error;

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }
}
