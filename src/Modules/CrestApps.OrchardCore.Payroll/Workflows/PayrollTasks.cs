using System.Globalization;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Payroll.Workflows;

/// <summary>
/// The base of Payroll's tasks: settings are Liquid, a run is found by its id or its number, and the task acts as the
/// person the workflow runs for (none for a timer), with the same rules as on the pages.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public abstract class PayrollTaskActivity<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    protected PayrollTaskActivity(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer stringLocalizer)
    {
        Services = services;
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    protected IServiceProvider Services { get; }

    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Payroll"];

    /// <summary>Gets or sets the run's id or number.</summary>
    public string PayRunId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    protected System.Security.Claims.ClaimsPrincipal User
        => Services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

    /// <summary>
    /// Gets or sets whether the run's amounts (gross, net, cash needed) are written to the output: "true" asks for
    /// them, and they are written only when the workflow's user may view the employer's payroll reports.
    /// </summary>
    public string IncludeTotals
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    protected async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    // The run named by the PayRunId setting: by its id, else by its number.
    protected async Task<PayRun> FindRunAsync(WorkflowExecutionContext workflowContext)
    {
        if (await EvaluateAsync(PayRunId, workflowContext) is not { } key)
        {
            return null;
        }

        var runs = Services.GetRequiredService<IPayRunManager>();

        if (await runs.FindByIdAsync(key) is { } run)
        {
            return run;
        }

        var employers = (await Services.GetRequiredService<EmployerStore>().GetAllAsync()).Select(employer => employer.ItemId).ToList();

        return (await runs.ListByEmployersAsync(employers)).FirstOrDefault(candidate => string.Equals(candidate.Number, key, StringComparison.OrdinalIgnoreCase));
    }

    protected ActivityExecutionResult Fail(WorkflowExecutionContext workflowContext, string outcome, IEnumerable<string> errors)
    {
        workflowContext.LastResult = string.Join(" ", errors);

        return Outcome(outcome);
    }

    // Workflow state is kept and shown to whoever manages workflows: the run's amounts only when the task asks for them
    // and its user may see them; a timer has no user, so never.
    protected async Task WriteTotalsAsync(WorkflowExecutionContext workflowContext, PayRun run)
    {
        workflowContext.Output["PayRunId"] = run.ItemId;
        workflowContext.Output["PayRunNumber"] = run.Number;
        workflowContext.Output["Statements"] = run.Totals.Statements;

        if (string.Equals(IncludeTotals, "true", StringComparison.OrdinalIgnoreCase)
            && User is { Identity.IsAuthenticated: true } user
            && await Services.GetRequiredService<IPayrollScopeService>().AuthorizeAsync(user, PayrollPermissions.ViewPayrollReports, run.EmployerId))
        {
            workflowContext.Output["Gross"] = run.Totals.Gross;
            workflowContext.Output["Net"] = run.Totals.Net;
            workflowContext.Output["CashNeeded"] = run.Totals.CashNeeded;
        }
    }
}

/// <summary>
/// Adds a pay run for an employer and a pay schedule, once per period.
/// </summary>
public sealed class OpenPayRunTask : PayrollTaskActivity<OpenPayRunTask>
{
    public OpenPayRunTask(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<OpenPayRunTask> stringLocalizer)
        : base(services, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Open Pay Run"];

    public string EmployerId { get => GetProperty<string>(); set => SetProperty(value); }

    public string PayScheduleId { get => GetProperty<string>(); set => SetProperty(value); }

    public string RunType { get => GetProperty<string>(); set => SetProperty(value); }

    public string PeriodStart { get => GetProperty<string>(); set => SetProperty(value); }

    public string PeriodEnd { get => GetProperty<string>(); set => SetProperty(value); }

    public string PayDate { get => GetProperty<string>(); set => SetProperty(value); }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Exists"]), new Outcome(S["Failed"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var runs = Services.GetRequiredService<IPayRunManager>();
        var run = await runs.NewAsync();

        run.EmployerId = await EvaluateAsync(EmployerId, workflowContext);
        run.PayScheduleId = await EvaluateAsync(PayScheduleId, workflowContext);
        run.Type = Enum.TryParse<PayRunType>(await EvaluateAsync(RunType, workflowContext), ignoreCase: true, out var type) ? type : PayRunType.Regular;

        if (string.IsNullOrEmpty(run.PayScheduleId) || await Services.GetRequiredService<IPayScheduleManager>().FindByIdAsync(run.PayScheduleId) is not { } schedule)
        {
            return Fail(workflowContext, "Failed", [S["Choose the pay schedule."]]);
        }

        var today = DateOnly.FromDateTime((await Services.GetRequiredService<ILocalClock>().GetLocalNowAsync()).DateTime);
        var (start, end) = schedule.PeriodOn(today);

        run.PeriodStart = ParseDate(await EvaluateAsync(PeriodStart, workflowContext)) ?? start;
        run.PeriodEnd = ParseDate(await EvaluateAsync(PeriodEnd, workflowContext)) ?? end;
        run.PayDate = ParseDate(await EvaluateAsync(PayDate, workflowContext)) ?? schedule.PayDateFor(run.PeriodEnd);

        // A daily timer may run this every day: the period's run is added once.
        if ((await runs.ListByEmployersAsync([run.EmployerId ?? string.Empty])).FirstOrDefault(existing => existing.Status != PayRunStatus.Canceled
            && existing.PayScheduleId == run.PayScheduleId && existing.Type == run.Type && existing.PeriodStart == run.PeriodStart && existing.PeriodEnd == run.PeriodEnd) is { } open)
        {
            workflowContext.Output["PayRunId"] = open.ItemId;
            workflowContext.Output["PayRunNumber"] = open.Number;
            workflowContext.LastResult = open.ItemId;

            return Outcome("Exists");
        }

        var result = await Services.GetRequiredService<IPayRunService>().OpenAsync(run, User);

        if (!result.Succeeded)
        {
            return Fail(workflowContext, "Failed", result.Errors);
        }

        workflowContext.Output["PayRunId"] = run.ItemId;
        workflowContext.Output["PayRunNumber"] = run.Number;
        workflowContext.LastResult = run.ItemId;

        return Outcome("Done");
    }

    private static DateOnly? ParseDate(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}

/// <summary>
/// Adds hours or an amount to an open pay run for one person, as a file row would.
/// </summary>
public sealed class AddPayInputTask : PayrollTaskActivity<AddPayInputTask>
{
    public AddPayInputTask(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<AddPayInputTask> stringLocalizer)
        : base(services, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Add Pay Input"];

    public string EmployeeNumber { get => GetProperty<string>(); set => SetProperty(value); }

    public string PayCode { get => GetProperty<string>(); set => SetProperty(value); }

    public string Hours { get => GetProperty<string>(); set => SetProperty(value); }

    public string Amount { get => GetProperty<string>(); set => SetProperty(value); }

    public string Note { get => GetProperty<string>(); set => SetProperty(value); }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var run = await FindRunAsync(workflowContext);
        var importer = Services.GetRequiredService<PayRunInputImporter>();
        var error = await importer.SaveAsync(new PayRunInputRow
        {
            PayRun = run?.Number ?? await EvaluateAsync(PayRunId, workflowContext),
            EmployeeNumber = await EvaluateAsync(EmployeeNumber, workflowContext),
            PayCode = await EvaluateAsync(PayCode, workflowContext),
            Hours = await EvaluateAsync(Hours, workflowContext),
            Amount = await EvaluateAsync(Amount, workflowContext),
            Note = await EvaluateAsync(Note, workflowContext),

            // The same workflow saving the same row again (a retry) updates it; another workflow's row is its own.
            Key = "Workflow " + workflowContext.WorkflowId,
        }, User, S["Workflow"]);

        if (error is not null)
        {
            return Fail(workflowContext, "Failed", [error]);
        }

        await importer.RecalculateAsync(User);

        return Outcome("Done");
    }
}

/// <summary>
/// Calculates a pay run.
/// </summary>
public sealed class CalculatePayRunTask : PayrollTaskActivity<CalculatePayRunTask>
{
    public CalculatePayRunTask(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<CalculatePayRunTask> stringLocalizer)
        : base(services, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calculate Pay Run"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Blocked"]), new Outcome(S["Failed"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (await FindRunAsync(workflowContext) is not { } run)
        {
            return Fail(workflowContext, "Failed", [S["There is no such pay run."]]);
        }

        var result = await Services.GetRequiredService<IPayRunService>().CalculateAsync(run, User);

        if (!result.Succeeded)
        {
            return Fail(workflowContext, "Failed", result.Errors);
        }

        var calculated = await Services.GetRequiredService<IPayRunManager>().FindByIdAsync(run.ItemId) ?? run;

        await WriteTotalsAsync(workflowContext, calculated);

        return calculated.Issues.Any(issue => issue.IsBlocker)
            ? Fail(workflowContext, "Blocked", calculated.Issues.Where(issue => issue.IsBlocker).Select(issue => issue.Message))
            : Outcome("Done");
    }
}

/// <summary>
/// Submits a calculated pay run for approval.
/// </summary>
public sealed class SubmitPayRunTask : PayrollTaskActivity<SubmitPayRunTask>
{
    public SubmitPayRunTask(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<SubmitPayRunTask> stringLocalizer)
        : base(services, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Submit Pay Run for Approval"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (await FindRunAsync(workflowContext) is not { } run)
        {
            return Fail(workflowContext, "Failed", [S["There is no such pay run."]]);
        }

        var result = await Services.GetRequiredService<IPayRunService>().SubmitAsync(run, User);

        if (!result.Succeeded)
        {
            return Fail(workflowContext, "Failed", result.Errors);
        }

        var submitted = await Services.GetRequiredService<IPayRunManager>().FindByIdAsync(run.ItemId) ?? run;

        workflowContext.Output["ApprovalRequestId"] = submitted.ApprovalRequestId;
        workflowContext.LastResult = submitted.ApprovalRequestId;

        return Outcome("Done");
    }
}

/// <summary>
/// Decides a pay run's pending approval as the person the workflow runs for.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public abstract class DecidePayRunTask<TActivity> : PayrollTaskActivity<TActivity>
    where TActivity : ITask
{
    protected DecidePayRunTask(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer stringLocalizer)
        : base(services, expressionEvaluator, stringLocalizer)
    {
    }

    public string Note { get => GetProperty<string>(); set => SetProperty(value); }

    protected abstract ApprovalDecisionKind Decision { get; }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["NotAllowed"]), new Outcome(S["Failed"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (await FindRunAsync(workflowContext) is not { } run || string.IsNullOrEmpty(run.ApprovalRequestId)
            || Services.GetService<IApprovalRequestManager>() is not { } requests || Services.GetService<IApprovalService>() is not { } approvals
            || await requests.FindByIdAsync(run.ApprovalRequestId) is not { Status: ApprovalStatus.Pending } request)
        {
            return Fail(workflowContext, "Failed", [S["The pay run has no approval waiting."]]);
        }

        var eligibility = await approvals.GetEligibilityAsync(request, User);

        if (!eligibility.CanDecide)
        {
            return Fail(workflowContext, "NotAllowed", [eligibility.Reason]);
        }

        var result = await approvals.DecideAsync(request, User, Decision, await EvaluateAsync(Note, workflowContext));

        return result.Succeeded ? Outcome("Done") : Fail(workflowContext, "Failed", result.Errors);
    }
}

/// <summary>
/// Approves a pay run's pending approval.
/// </summary>
public sealed class ApprovePayRunTask : DecidePayRunTask<ApprovePayRunTask>
{
    public ApprovePayRunTask(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<ApprovePayRunTask> stringLocalizer)
        : base(services, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approve Pay Run"];

    protected override ApprovalDecisionKind Decision => ApprovalDecisionKind.Approve;
}

/// <summary>
/// Rejects a pay run's pending approval.
/// </summary>
public sealed class RejectPayRunTask : DecidePayRunTask<RejectPayRunTask>
{
    public RejectPayRunTask(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<RejectPayRunTask> stringLocalizer)
        : base(services, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Reject Pay Run"];

    protected override ApprovalDecisionKind Decision => ApprovalDecisionKind.Reject;
}

/// <summary>
/// Processes an approved pay run, or pays what is left of a partly paid one.
/// </summary>
public sealed class ProcessPayRunTask : PayrollTaskActivity<ProcessPayRunTask>
{
    public ProcessPayRunTask(IServiceProvider services, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<ProcessPayRunTask> stringLocalizer)
        : base(services, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Process Pay Run"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Paid"]), new Outcome(S["PartiallyPaid"]), new Outcome(S["Failed"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (await FindRunAsync(workflowContext) is not { } run)
        {
            return Fail(workflowContext, "Failed", [S["There is no such pay run."]]);
        }

        var result = await Services.GetRequiredService<IPayRunService>().ProcessAsync(run, User);
        var processed = await Services.GetRequiredService<IPayRunManager>().FindByIdAsync(run.ItemId) ?? run;

        await WriteTotalsAsync(workflowContext, processed);
        workflowContext.LastResult = string.Join(" ", result.Errors);

        return processed.Status switch
        {
            PayRunStatus.Paid => Outcome("Paid"),
            PayRunStatus.PartiallyPaid => Outcome("PartiallyPaid"),
            _ => Outcome("Failed"),
        };
    }
}
