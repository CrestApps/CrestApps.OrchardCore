using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

/// <summary>
/// Starts a person on the certification path of a certification, such as a new hire.
/// </summary>
public sealed class StartCertificationPathTask : TaskActivity<StartCertificationPathTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ICertificationManager _certifications;
    private readonly ICertificationPathService _paths;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartCertificationPathTask"/> class.
    /// </summary>
    public StartCertificationPathTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        ICertificationManager certifications,
        ICertificationPathService paths,
        IStringLocalizer<StartCertificationPathTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _certifications = certifications;
        _paths = paths;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category => S["Learning"];

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Start Certification Path"];

    /// <summary>
    /// Gets or sets the certification, by name or id. Liquid is allowed.
    /// </summary>
    public string Certification
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the user id. Liquid is allowed.
    /// </summary>
    public string UserId
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
        var certificationText = await EvaluateAsync(Certification, workflowContext);
        var userId = await EvaluateAsync(UserId, workflowContext);
        var certification = string.IsNullOrEmpty(certificationText)
            ? null
            : await _certifications.FindByNameAsync(certificationText) ?? await _certifications.FindByIdAsync(certificationText);
        var path = certification is null ? null : await _paths.FindByCertificationAsync(certification.ItemId);

        if (path is null || string.IsNullOrEmpty(userId))
        {
            workflowContext.LastResult = "The certification has no path, or the user is missing.";

            return Outcome("Failed");
        }

        var result = await _paths.StartAsync(path.ItemId, userId, "Workflow");

        workflowContext.LastResult = result.Succeeded ? result.Progress?.ItemId : string.Join(" ", result.Errors);

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}
