using System.Globalization;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// Gives a person a skill, or changes how well they hold it. Its fields are Liquid expressions.
/// </summary>
public sealed class AssignSkillTask : TaskActivity<AssignSkillTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ISkillManager _skillManager;
    private readonly ISkillAssignmentService _assignmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssignSkillTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="skillManager">The skill manager.</param>
    /// <param name="assignmentService">The skill assignment service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssignSkillTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        IStringLocalizer<AssignSkillTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _skillManager = skillManager;
        _assignmentService = assignmentService;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Skills"];

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Assign Skill"];

    /// <summary>
    /// Gets or sets who holds the skill: <c>User</c> or <c>Worker</c>, a Liquid expression.
    /// </summary>
    public string SubjectKind
    {
        get => GetProperty(() => nameof(SkillSubjectKind.User));
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the user id or worker id, a Liquid expression.
    /// </summary>
    public string SubjectId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the skill, by name or id, a Liquid expression.
    /// </summary>
    public string Skill
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the proficiency from 1 to 5, a Liquid expression; 0 takes the skill away.
    /// </summary>
    public string Proficiency
    {
        get => GetProperty(() => "3");
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var kind = Enum.TryParse<SkillSubjectKind>(await EvaluateAsync(SubjectKind, workflowContext), ignoreCase: true, out var parsed)
            ? parsed
            : SkillSubjectKind.User;
        var subject = new SkillSubject(kind, await EvaluateAsync(SubjectId, workflowContext));
        var skillText = await EvaluateAsync(Skill, workflowContext);
        var skill = string.IsNullOrEmpty(skillText)
            ? null
            : await _skillManager.FindByNameAsync(skillText) ?? await _skillManager.FindByIdAsync(skillText);

        if (subject.IsEmpty || skill is null)
        {
            workflowContext.LastResult = "The person or the skill is missing.";

            return Outcome("Failed");
        }

        var proficiency = int.TryParse(await EvaluateAsync(Proficiency, workflowContext), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : SkillProficiency.Default;

        if (proficiency == 0)
        {
            await _assignmentService.RemoveAsync(subject, skill.ItemId, "workflow");

            return Outcome("Done");
        }

        var result = await _assignmentService.AssignAsync(subject, new SkillAssignmentInput { SkillId = skill.ItemId, Proficiency = proficiency }, "workflow");

        if (!result.Succeeded)
        {
            workflowContext.LastResult = string.Join(" ", result.Errors);

            return Outcome("Failed");
        }

        return Outcome("Done");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
