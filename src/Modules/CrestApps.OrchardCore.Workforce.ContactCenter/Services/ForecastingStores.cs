using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services;

/// <summary>
/// Stores planning groups.
/// </summary>
public sealed class PlanningGroupStore : DocumentCatalog<PlanningGroup, PlanningGroupIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlanningGroupStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public PlanningGroupStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }
}

/// <summary>
/// Manages planning groups.
/// </summary>
public sealed class PlanningGroupManager : CatalogManager<PlanningGroup>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlanningGroupManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PlanningGroupManager(PlanningGroupStore store, IEnumerable<ICatalogEntryHandler<PlanningGroup>> handlers, ILogger<CatalogManager<PlanningGroup>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Stores forecast events.
/// </summary>
public sealed class ForecastEventStore : DocumentCatalog<ForecastEvent, ForecastEventIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastEventStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public ForecastEventStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }
}

/// <summary>
/// Manages forecast events.
/// </summary>
public sealed class ForecastEventManager : CatalogManager<ForecastEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastEventManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public ForecastEventManager(ForecastEventStore store, IEnumerable<ICatalogEntryHandler<ForecastEvent>> handlers, ILogger<CatalogManager<ForecastEvent>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Stores capacity plans.
/// </summary>
public sealed class CapacityPlanStore : DocumentCatalog<CapacityPlan, CapacityPlanIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityPlanStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public CapacityPlanStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }
}

/// <summary>
/// Manages capacity plans.
/// </summary>
public sealed class CapacityPlanManager : CatalogManager<CapacityPlan>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityPlanManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public CapacityPlanManager(CapacityPlanStore store, IEnumerable<ICatalogEntryHandler<CapacityPlan>> handlers, ILogger<CatalogManager<CapacityPlan>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Validates and stamps capacity plans.
/// </summary>
internal sealed class CapacityPlanHandler : CatalogEntryHandlerBase<CapacityPlan>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityPlanHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CapacityPlanHandler(IClock clock, IStringLocalizer<CapacityPlanHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<CapacityPlan> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<CapacityPlan> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<CapacityPlan> context, CancellationToken cancellationToken = default)
    {
        var plan = context.Model;

        if (string.IsNullOrWhiteSpace(plan.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(CapacityPlan.Name)]));
        }

        if (string.IsNullOrEmpty(plan.PlanningGroupId))
        {
            context.Result.Fail(new ValidationResult(S["Choose a planning group."], [nameof(CapacityPlan.PlanningGroupId)]));
        }

        if (plan.Weeks is < 1 or > CapacityPlanner.MaxWeeks)
        {
            context.Result.Fail(new ValidationResult(S["A plan covers 1 to {0} weeks.", CapacityPlanner.MaxWeeks], [nameof(CapacityPlan.Weeks)]));
        }

        if (plan.HoursPerFte is <= 0 or > 80)
        {
            context.Result.Fail(new ValidationResult(S["A full-time agent's hours must be more than 0 and at most 80 a week."], [nameof(CapacityPlan.HoursPerFte)]));
        }

        if (plan.CurrentFte < 0 || plan.WeeklyWorkloadHours < 0)
        {
            context.Result.Fail(new ValidationResult(S["The agents and the work can't be negative."], [nameof(CapacityPlan.CurrentFte)]));
        }

        if (plan.AttritionPercentPerMonth is < 0 or >= 100)
        {
            context.Result.Fail(new ValidationResult(S["Attrition must be at least 0 and under 100 percent a month."], [nameof(CapacityPlan.AttritionPercentPerMonth)]));
        }

        if (plan.GrowthPercentPerYear <= -100)
        {
            context.Result.Fail(new ValidationResult(S["The work can't shrink by 100 percent or more."], [nameof(CapacityPlan.GrowthPercentPerYear)]));
        }

        if (plan.HiringClasses.Any(item => item.Size <= 0 || item.TrainingWeeks < 0 || item.RampWeeks < 0))
        {
            context.Result.Fail(new ValidationResult(S["Each hiring class needs a size above 0, and its training and ramp-up can't be negative."], [nameof(CapacityPlan.HiringClasses)]));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Validates and stamps planning groups.
/// </summary>
internal sealed class PlanningGroupHandler : CatalogEntryHandlerBase<PlanningGroup>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanningGroupHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PlanningGroupHandler(IClock clock, IStringLocalizer<PlanningGroupHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<PlanningGroup> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<PlanningGroup> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<PlanningGroup> context, CancellationToken cancellationToken = default)
    {
        var group = context.Model;

        if (string.IsNullOrWhiteSpace(group.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(PlanningGroup.Name)]));
        }

        if (group.QueueIds.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Choose at least one queue."], [nameof(PlanningGroup.QueueIds)]));
        }

        if (group.ServiceLevelPercent is null && group.AverageSpeedOfAnswerSeconds is null)
        {
            context.Result.Fail(new ValidationResult(S["Set a service level, an average speed of answer, or both."], [nameof(PlanningGroup.ServiceLevelPercent)]));
        }

        if (group.ServiceLevelPercent is <= 0 or >= 100)
        {
            context.Result.Fail(new ValidationResult(S["The service level must be between 0 and 100 percent."], [nameof(PlanningGroup.ServiceLevelPercent)]));
        }

        if (group.MaxOccupancyPercent is <= 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["The highest occupancy must be between 0 and 100 percent."], [nameof(PlanningGroup.MaxOccupancyPercent)]));
        }

        if (group.ShrinkagePercent is < 0 or >= 100)
        {
            context.Result.Fail(new ValidationResult(S["Shrinkage must be at least 0 and under 100 percent."], [nameof(PlanningGroup.ShrinkagePercent)]));
        }

        if (group.Concurrency < 1 || group.HistoryWeeks < 1)
        {
            context.Result.Fail(new ValidationResult(S["Concurrency and weeks of history must be at least 1."], [nameof(PlanningGroup.Concurrency)]));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Validates forecast events.
/// </summary>
internal sealed class ForecastEventHandler : CatalogEntryHandlerBase<ForecastEvent>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastEventHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ForecastEventHandler(IStringLocalizer<ForecastEventHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<ForecastEvent> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ForecastEvent.Name)]));
        }

        if (context.Model.EndDate < context.Model.StartDate)
        {
            context.Result.Fail(new ValidationResult(S["The last day must not be before the first day."], [nameof(ForecastEvent.EndDate)]));
        }

        if (context.Model.VolumeMultiplier < 0)
        {
            context.Result.Fail(new ValidationResult(S["The volume multiplier can't be negative."], [nameof(ForecastEvent.VolumeMultiplier)]));
        }

        return Task.CompletedTask;
    }
}
