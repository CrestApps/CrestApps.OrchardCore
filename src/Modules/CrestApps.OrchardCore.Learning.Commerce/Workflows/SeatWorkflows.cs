using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Commerce.Workflows;

public abstract class SeatEventActivity : EventActivity
{
    protected SeatEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    public override LocalizedString Category => S["Learning"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => [new Outcome(S["Done"])];

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext) => Outcome("Done");
}

public sealed class SeatsPurchasedEvent : SeatEventActivity
{
    public SeatsPurchasedEvent(IStringLocalizer<SeatsPurchasedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(SeatsPurchasedEvent);

    public override LocalizedString DisplayText => S["Course Seats Purchased"];
}

public sealed class SeatGivenEvent : SeatEventActivity
{
    public SeatGivenEvent(IStringLocalizer<SeatGivenEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(SeatGivenEvent);

    public override LocalizedString DisplayText => S["Course Seat Given"];
}

public sealed class LicensePoolRunningLowEvent : SeatEventActivity
{
    public LicensePoolRunningLowEvent(IStringLocalizer<LicensePoolRunningLowEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(LicensePoolRunningLowEvent);

    public override LocalizedString DisplayText => S["Licence Pool Running Low"];
}

/// <summary>
/// Gives a seat of a pool to a member of its account.
/// </summary>
public sealed class GiveSeatTask : TaskActivity<GiveSeatTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILicensePoolService _pools;

    public GiveSeatTask(IWorkflowExpressionEvaluator expressionEvaluator, ILicensePoolService pools, IStringLocalizer<GiveSeatTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _pools = pools;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override LocalizedString Category => S["Learning"];

    public override LocalizedString DisplayText => S["Give Course Seat"];

    public string PoolId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string UserId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var poolId = await EvaluateAsync(PoolId, workflowContext);
        var userId = await EvaluateAsync(UserId, workflowContext);
        var result = await _pools.GiveAsync(poolId, userId, "Workflow");

        workflowContext.LastResult = result.Succeeded ? result.Seat.ItemId : result.Error;

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression) ? null : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}

public class GiveSeatTaskViewModel
{
    public string PoolId { get; set; }

    public string UserId { get; set; }
}

public sealed class SeatsPurchasedEventDisplayDriver : ActivityDisplayDriver<SeatsPurchasedEvent>
{
}

public sealed class SeatGivenEventDisplayDriver : ActivityDisplayDriver<SeatGivenEvent>
{
}

public sealed class LicensePoolRunningLowEventDisplayDriver : ActivityDisplayDriver<LicensePoolRunningLowEvent>
{
}

public sealed class GiveSeatTaskDisplayDriver : ActivityDisplayDriver<GiveSeatTask, GiveSeatTaskViewModel>
{
    protected override void EditActivity(GiveSeatTask activity, GiveSeatTaskViewModel model)
    {
        model.PoolId = activity.PoolId;
        model.UserId = activity.UserId;
    }

    protected override void UpdateActivity(GiveSeatTaskViewModel model, GiveSeatTask activity)
    {
        activity.PoolId = model.PoolId?.Trim();
        activity.UserId = model.UserId?.Trim();
    }
}

/// <summary>
/// Triggers the seat workflow events after the request commits.
/// </summary>
internal sealed class SeatWorkflowObserver : ILicensePoolObserver
{
    public Task SeatsPurchasedAsync(LicensePool pool, CancellationToken cancellationToken = default)
        => Trigger(nameof(SeatsPurchasedEvent), pool, null, null);

    public Task SeatGivenAsync(LicensePool pool, LicenseSeat seat, CancellationToken cancellationToken = default)
        => Trigger(nameof(SeatGivenEvent), pool, seat, null);

    public Task RunningLowAsync(LicensePool pool, int remaining, CancellationToken cancellationToken = default)
        => Trigger(nameof(LicensePoolRunningLowEvent), pool, null, remaining);

    private static Task Trigger(string name, LicensePool pool, LicenseSeat seat, int? remaining)
    {
        var input = new Dictionary<string, object>
        {
            ["PoolId"] = pool.ItemId,
            ["AccountId"] = pool.AccountId,
            ["CourseId"] = pool.CourseId,
            ["Seats"] = pool.Seats,
            ["SeatId"] = seat?.ItemId,
            ["UserId"] = seat?.UserId,
            ["Remaining"] = remaining,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, pool.ItemId));

        return Task.CompletedTask;
    }
}

internal abstract class SeatEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Learning";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context) => [];
}

internal sealed class SeatsPurchasedEventSchema : SeatEventSchemaBase
{
    public override string Name => "SeatsPurchasedEvent";

    protected override string DisplayText => "Course Seats Purchased";

    protected override string Description => "Starts when an account buys seats in a course";
}

internal sealed class SeatGivenEventSchema : SeatEventSchemaBase
{
    public override string Name => "SeatGivenEvent";

    protected override string DisplayText => "Course Seat Given";

    protected override string Description => "Starts when a seat in a course is given to a person";
}

internal sealed class LicensePoolRunningLowEventSchema : SeatEventSchemaBase
{
    public override string Name => "LicensePoolRunningLowEvent";

    protected override string DisplayText => "Licence Pool Running Low";

    protected override string Description => "Starts when an account has a tenth of its seats in a course or fewer left";
}

internal sealed class GiveSeatTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "GiveSeatTask";

    protected override string Category => "Learning";

    protected override string DisplayText => "Give Course Seat";

    protected override string Description => "Gives a seat of a licence pool to a member of its account";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["PoolId", "UserId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PoolId", WorkflowActivitySchemaBuilders.String("The licence pool. A plain string that supports Liquid."));
        yield return ("UserId", WorkflowActivitySchemaBuilders.String("The user id of the member. A plain string that supports Liquid."));
    }
}
