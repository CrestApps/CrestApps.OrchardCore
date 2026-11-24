using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Programs.Workflows;

/// <summary>
/// An event about a person under required learning, optionally limited to some rules.
/// </summary>
public abstract class RequiredLearningEventActivity : EventActivity
{
    protected RequiredLearningEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    public override LocalizedString Category => S["Learning"];

    /// <summary>
    /// Gets or sets the rules that start the workflow; empty for every rule.
    /// </summary>
    public IList<string> RequirementIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => RequirementIds.Count == 0
            || (workflowContext.Input.TryGetValue("RequirementId", out var requirementId) && RequirementIds.Contains(requirementId?.ToString() ?? string.Empty));

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

public sealed class LearningRequiredEvent : RequiredLearningEventActivity
{
    public LearningRequiredEvent(IStringLocalizer<LearningRequiredEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(LearningRequiredEvent);

    public override LocalizedString DisplayText => S["Learning Required"];
}

public sealed class RequiredLearningDueSoonEvent : RequiredLearningEventActivity
{
    public RequiredLearningDueSoonEvent(IStringLocalizer<RequiredLearningDueSoonEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(RequiredLearningDueSoonEvent);

    public override LocalizedString DisplayText => S["Required Learning Due Soon"];
}

public sealed class RequiredLearningOverdueEvent : RequiredLearningEventActivity
{
    public RequiredLearningOverdueEvent(IStringLocalizer<RequiredLearningOverdueEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(RequiredLearningOverdueEvent);

    public override LocalizedString DisplayText => S["Required Learning Overdue"];
}

public sealed class RequiredLearningExemptionGrantedEvent : RequiredLearningEventActivity
{
    public RequiredLearningExemptionGrantedEvent(IStringLocalizer<RequiredLearningExemptionGrantedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(RequiredLearningExemptionGrantedEvent);

    public override LocalizedString DisplayText => S["Required Learning Exemption Granted"];
}

/// <summary>
/// Starts when a person completes a learning path, optionally only some paths.
/// </summary>
public sealed class LearningPathCompletedEvent : EventActivity
{
    public LearningPathCompletedEvent(IStringLocalizer<LearningPathCompletedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override string Name => nameof(LearningPathCompletedEvent);

    public override LocalizedString Category => S["Learning"];

    public override LocalizedString DisplayText => S["Learning Path Completed"];

    /// <summary>
    /// Gets or sets the content item ids of the paths that start the workflow; empty for every path.
    /// </summary>
    public IList<string> PathIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => PathIds.Count == 0
            || (workflowContext.Input.TryGetValue("PathId", out var pathId) && PathIds.Contains(pathId?.ToString() ?? string.Empty));

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Requires learning of a person: adds them to the named people of a rule and runs the rule.
/// </summary>
public sealed class RequireLearningTask : TaskActivity<RequireLearningTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IRequiredLearningService _requiredLearning;

    public RequireLearningTask(IWorkflowExpressionEvaluator expressionEvaluator, IRequiredLearningService requiredLearning, IStringLocalizer<RequireLearningTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _requiredLearning = requiredLearning;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override LocalizedString Category => S["Learning"];

    public override LocalizedString DisplayText => S["Require Learning"];

    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public string RequirementId
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

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var userId = string.IsNullOrWhiteSpace(UserId) ? null : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(UserId), workflowContext, null))?.Trim();
        var requirement = await _requiredLearning.FindByIdAsync(RequirementId);

        if (requirement is null || string.IsNullOrEmpty(userId))
        {
            workflowContext.LastResult = "The rule was not found, or the user is missing.";

            return Outcome("Failed");
        }

        if (!requirement.UserIds.Contains(userId))
        {
            requirement.UserIds.Add(userId);

            var errors = await _requiredLearning.SaveAsync(requirement);

            if (errors.Count > 0)
            {
                workflowContext.LastResult = string.Join(" ", errors);

                return Outcome("Failed");
            }
        }

        var result = await _requiredLearning.RunAsync(requirement.ItemId, "Workflow");

        workflowContext.LastResult = result.Covered;

        return Outcome("Done");
    }
}

/// <summary>
/// Exempts a person from a rule with a reason.
/// </summary>
public sealed class GrantRequiredLearningExemptionTask : TaskActivity<GrantRequiredLearningExemptionTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IRequiredLearningService _requiredLearning;

    public GrantRequiredLearningExemptionTask(IWorkflowExpressionEvaluator expressionEvaluator, IRequiredLearningService requiredLearning, IStringLocalizer<GrantRequiredLearningExemptionTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _requiredLearning = requiredLearning;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override LocalizedString Category => S["Learning"];

    public override LocalizedString DisplayText => S["Grant Required Learning Exemption"];

    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public string RequirementId
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
    /// Gets or sets the reason, as a Liquid expression.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var userId = string.IsNullOrWhiteSpace(UserId) ? null : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(UserId), workflowContext, null))?.Trim();
        var reason = string.IsNullOrWhiteSpace(Reason) ? null : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Reason), workflowContext, null))?.Trim();
        var assignment = (await _requiredLearning.ListForUsersAsync(string.IsNullOrEmpty(userId) ? [] : [userId]))
            .FirstOrDefault(candidate => candidate.RequirementId == RequirementId);

        if (assignment is null || string.IsNullOrEmpty(reason))
        {
            workflowContext.LastResult = "The person is not under the rule, or the reason is missing.";

            return Outcome("Failed");
        }

        var exempt = await _requiredLearning.ExemptAsync(assignment.ItemId, new RequiredLearningExemption { Reason = reason, GrantedBy = "Workflow" });

        workflowContext.LastResult = exempt?.ItemId;

        return Outcome(exempt is null ? "Failed" : "Done");
    }
}
