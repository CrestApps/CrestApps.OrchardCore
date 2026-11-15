using CrestApps.OrchardCore.Learning.Certifications.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

/// <summary>
/// Starts when education credits are awarded.
/// </summary>
public sealed class EducationCreditsAwardedEvent : EventActivity
{
    public EducationCreditsAwardedEvent(IStringLocalizer<EducationCreditsAwardedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override string Name => nameof(EducationCreditsAwardedEvent);

    public override LocalizedString Category => S["Learning"];

    public override LocalizedString DisplayText => S["Education Credits Awarded"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Starts when a person records external learning.
/// </summary>
public sealed class ExternalLearningSubmittedEvent : EventActivity
{
    public ExternalLearningSubmittedEvent(IStringLocalizer<ExternalLearningSubmittedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override string Name => nameof(ExternalLearningSubmittedEvent);

    public override LocalizedString Category => S["Learning"];

    public override LocalizedString DisplayText => S["External Learning Submitted"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Awards education credits to a person, once per source id.
/// </summary>
public sealed class AwardEducationCreditsTask : TaskActivity<AwardEducationCreditsTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IEducationCreditService _credits;
    private readonly ILocalClock _localClock;

    public AwardEducationCreditsTask(IWorkflowExpressionEvaluator expressionEvaluator, IEducationCreditService credits, ILocalClock localClock, IStringLocalizer<AwardEducationCreditsTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _credits = credits;
        _localClock = localClock;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override LocalizedString Category => S["Learning"];

    public override LocalizedString DisplayText => S["Award Education Credits"];

    /// <summary>
    /// Gets or sets the credit type, by name or id.
    /// </summary>
    public string CreditType
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the person, as a Liquid expression.
    /// </summary>
    public string UserId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the amount, as a Liquid expression.
    /// </summary>
    public string Amount
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets what the credits are for, which makes the award happen once; a Liquid expression.
    /// </summary>
    public string SourceId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var typeText = await EvaluateAsync(CreditType, workflowContext);
        var userId = await EvaluateAsync(UserId, workflowContext);
        var sourceId = await EvaluateAsync(SourceId, workflowContext);
        var types = await _credits.ListTypesAsync();
        var type = types.FirstOrDefault(candidate => candidate.ItemId == typeText) ?? types.FirstOrDefault(candidate => string.Equals(candidate.Name, typeText, StringComparison.OrdinalIgnoreCase));

        if (type is null || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sourceId)
            || !decimal.TryParse(await EvaluateAsync(Amount, workflowContext), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            workflowContext.LastResult = "The credit type, the user, the source or the amount is missing.";

            return Outcome("Failed");
        }

        var result = await _credits.AwardAsync(new EducationCreditAwardRequest
        {
            UserId = userId,
            CreditTypeId = type.ItemId,
            Amount = amount,
            EarnedOn = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime),
            Source = CertificationsConstants.CreditSources.Manual,
            SourceId = sourceId,
            Actor = "Workflow",
        });

        workflowContext.LastResult = result?.Entry?.ItemId;

        return Outcome(result is null ? "Failed" : "Done");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}
