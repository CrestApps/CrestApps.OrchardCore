using System.Globalization;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// A workflow task of the Approvals module. Its fields are Liquid expressions.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public abstract class ApprovalTaskActivity<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected ApprovalTaskActivity(IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Approvals"];

    /// <summary>
    /// Gets or sets the request id (Liquid).
    /// </summary>
    public string RequestId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Evaluates a Liquid field.
    /// </summary>
    /// <param name="expression">The field.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The value, trimmed, or <see langword="null"/>.</returns>
    protected async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}

/// <summary>
/// Asks for an approval under a policy. Writes the request id to the output <c>ApprovalRequestId</c>.
/// </summary>
public sealed class RequestApprovalTask : ApprovalTaskActivity<RequestApprovalTask>
{
    private readonly IApprovalService _approvalService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestApprovalTask"/> class.
    /// </summary>
    /// <param name="approvalService">The approval rules.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the person the workflow runs for.</param>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RequestApprovalTask(IApprovalService approvalService, IHttpContextAccessor httpContextAccessor, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<RequestApprovalTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _approvalService = approvalService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Request Approval"];

    /// <summary>
    /// Gets or sets the policy, by id or technical name (Liquid).
    /// </summary>
    public string Policy
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the subject's kind (Liquid).
    /// </summary>
    public string SubjectKind
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the subject's id (Liquid).
    /// </summary>
    public string SubjectId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the access scope (Liquid).
    /// </summary>
    public string AccessScope
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets what is asked (Liquid).
    /// </summary>
    public string Title
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the subject's page, application-relative (Liquid).
    /// </summary>
    public string SubjectUrl
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var result = await _approvalService.RequestAsync(new ApprovalSubmission
        {
            Policy = await EvaluateAsync(Policy, workflowContext),
            SubjectKind = await EvaluateAsync(SubjectKind, workflowContext),
            SubjectId = await EvaluateAsync(SubjectId, workflowContext),
            AccessScope = await EvaluateAsync(AccessScope, workflowContext),
            Title = await EvaluateAsync(Title, workflowContext),
            SubjectUrl = await EvaluateAsync(SubjectUrl, workflowContext),
        }, _httpContextAccessor.HttpContext?.User);

        if (!result.Succeeded)
        {
            workflowContext.LastResult = string.Join(" ", result.Errors);

            return Outcome("Failed");
        }

        workflowContext.Output["ApprovalRequestId"] = result.Request.ItemId;
        workflowContext.LastResult = result.Request.ItemId;

        return Outcome("Done");
    }
}

/// <summary>
/// The base of the tasks that decide a request. A workflow decides as the person it runs for, under the same policy
/// and the same two rules as a person on the page; a workflow that runs for nobody can't decide.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public abstract class DecideRequestTask<TActivity> : ApprovalTaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IApprovalRequestManager _requests;
    private readonly IApprovalService _approvalService;
    private readonly ISourceAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecideRequestTask{TActivity}"/> class.
    /// </summary>
    /// <param name="requests">The requests.</param>
    /// <param name="approvalService">The approval rules.</param>
    /// <param name="access">Who may act on the requests of each subject kind.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the person the workflow runs for.</param>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected DecideRequestTask(IApprovalRequestManager requests, IApprovalService approvalService, ISourceAccessService access, IHttpContextAccessor httpContextAccessor, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _requests = requests;
        _approvalService = approvalService;
        _access = access;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets or sets the approver's note (Liquid).
    /// </summary>
    public string Note
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets the decision the task makes.
    /// </summary>
    protected abstract ApprovalDecisionKind Decision { get; }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["NotAllowed"]), new Outcome(S["NotFound"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var id = await EvaluateAsync(RequestId, workflowContext);
        var user = _httpContextAccessor.HttpContext?.User;
        var request = string.IsNullOrEmpty(id) ? null : await _requests.FindByIdAsync(id);

        if (request is null || !await _access.CanAccessAsync(user, request.SubjectKind, request.AccessScope, SourceAccessLevel.Manage))
        {
            return Outcome("NotFound");
        }

        var result = await _approvalService.DecideAsync(request, user, Decision, await EvaluateAsync(Note, workflowContext));

        workflowContext.LastResult = result.Succeeded ? request.Status.ToString() : string.Join(" ", result.Errors);

        return Outcome(result.Succeeded ? "Done" : "NotAllowed");
    }
}

/// <summary>
/// Approves a request as the person the workflow runs for.
/// </summary>
public sealed class ApproveRequestTask : DecideRequestTask<ApproveRequestTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApproveRequestTask"/> class.
    /// </summary>
    /// <param name="requests">The requests.</param>
    /// <param name="approvalService">The approval rules.</param>
    /// <param name="access">Who may act on the requests of each subject kind.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApproveRequestTask(IApprovalRequestManager requests, IApprovalService approvalService, ISourceAccessService access, IHttpContextAccessor httpContextAccessor, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<ApproveRequestTask> stringLocalizer)
        : base(requests, approvalService, access, httpContextAccessor, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approve Request"];

    /// <inheritdoc />
    protected override ApprovalDecisionKind Decision => ApprovalDecisionKind.Approve;
}

/// <summary>
/// Rejects a request as the person the workflow runs for; the note is required.
/// </summary>
public sealed class RejectRequestTask : DecideRequestTask<RejectRequestTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RejectRequestTask"/> class.
    /// </summary>
    /// <param name="requests">The requests.</param>
    /// <param name="approvalService">The approval rules.</param>
    /// <param name="access">Who may act on the requests of each subject kind.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RejectRequestTask(IApprovalRequestManager requests, IApprovalService approvalService, ISourceAccessService access, IHttpContextAccessor httpContextAccessor, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<RejectRequestTask> stringLocalizer)
        : base(requests, approvalService, access, httpContextAccessor, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Reject Request"];

    /// <inheritdoc />
    protected override ApprovalDecisionKind Decision => ApprovalDecisionKind.Reject;
}

/// <summary>
/// Reads where a request is, such as before a reminder: outcomes Pending, Approved, Rejected, Canceled or NotFound,
/// and the output <c>ApprovalRequestStatus</c>.
/// </summary>
public sealed class GetApprovalRequestTask : ApprovalTaskActivity<GetApprovalRequestTask>
{
    private readonly IApprovalRequestManager _requests;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetApprovalRequestTask"/> class.
    /// </summary>
    /// <param name="requests">The requests.</param>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GetApprovalRequestTask(IApprovalRequestManager requests, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<GetApprovalRequestTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _requests = requests;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Get Approval Request"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Pending"]), new Outcome(S["Approved"]), new Outcome(S["Rejected"]), new Outcome(S["Canceled"]), new Outcome(S["NotFound"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var id = await EvaluateAsync(RequestId, workflowContext) ?? workflowContext.CorrelationId;
        var request = string.IsNullOrEmpty(id) ? null : await _requests.FindByIdAsync(id);

        if (request is null)
        {
            return Outcome("NotFound");
        }

        workflowContext.Output["ApprovalRequestStatus"] = request.Status.ToString();
        workflowContext.Output["ApprovalCount"] = request.ApprovalCount.ToString(CultureInfo.InvariantCulture);

        return Outcome(request.Status.ToString());
    }
}
