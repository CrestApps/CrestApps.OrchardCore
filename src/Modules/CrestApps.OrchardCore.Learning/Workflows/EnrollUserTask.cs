using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Enrolls a user in a course, or adds a workflow grant to the open enrollment the user has. The grant's source id is the
/// workflow instance, so a workflow that runs the task twice adds one grant.
/// </summary>
public sealed class EnrollUserTask : LearningTaskActivity<EnrollUserTask>
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly IUserSearchService _userSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollUserTask"/> class.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="userSearchService">The user search service, which checks the user exists.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EnrollUserTask(
        IEnrollmentService enrollmentService,
        IUserSearchService userSearchService,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<EnrollUserTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _enrollmentService = enrollmentService;
        _userSearchService = userSearchService;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Enroll User"];

    /// <summary>
    /// Gets or sets the user id, a Liquid expression.
    /// </summary>
    public string UserId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the content item id of the course, a Liquid expression.
    /// </summary>
    public string CourseId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets when access ends, an ISO 8601 time in a Liquid expression; empty for no end.
    /// </summary>
    public string AccessEnds
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets when the course is due, an ISO 8601 time in a Liquid expression; empty for no due date.
    /// </summary>
    public string Due
    {
        get => GetProperty<string>();
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
        var userId = await EvaluateAsync(UserId, workflowContext);
        var courseId = await EvaluateAsync(CourseId, workflowContext);

        if (userId is null || courseId is null || (await _userSearchService.GetAsync([userId])).Count == 0)
        {
            return Outcome("Failed");
        }

        var result = await _enrollmentService.EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Workflow,
            SourceId = workflowContext.WorkflowId,
            AccessEndsUtc = await EvaluateUtcAsync(AccessEnds, workflowContext),
            DueUtc = await EvaluateUtcAsync(Due, workflowContext),
            Actor = LearningEventActivity.WorkflowActor,
        });

        if (!result.Succeeded)
        {
            SetOutput(workflowContext, "Enrollment", new Dictionary<string, object> { ["Error"] = result.ErrorCode });

            return Outcome("Failed");
        }

        SetOutput(workflowContext, "Enrollment", new Dictionary<string, object>
        {
            ["EnrollmentId"] = result.Enrollment.ItemId,
            ["Number"] = result.Enrollment.Number,
            ["Status"] = result.Enrollment.Status.ToString(),
            ["Created"] = result.Created,
        });

        return Outcome("Done");
    }
}
