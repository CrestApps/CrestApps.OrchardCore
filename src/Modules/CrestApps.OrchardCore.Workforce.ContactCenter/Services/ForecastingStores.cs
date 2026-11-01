using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
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
