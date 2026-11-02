using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.TimeTracker.Core.Services;

/// <summary>
/// Keeps time categories.
/// </summary>
public sealed class TimeCategoryStore : DocumentCatalog<TimeCategory, TimeCategoryIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeCategoryStore"/> class.
    /// </summary>
    public TimeCategoryStore(ISession session)
        : base(session)
    {
        CollectionName = TimeTrackerConstants.CollectionName;
    }
}

/// <summary>
/// Keeps time stages.
/// </summary>
public sealed class TimeStageStore : DocumentCatalog<TimeStage, TimeStageIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeStageStore"/> class.
    /// </summary>
    public TimeStageStore(ISession session)
        : base(session)
    {
        CollectionName = TimeTrackerConstants.CollectionName;
    }
}

/// <summary>
/// Keeps time entries.
/// </summary>
public sealed class TimeEntryStore : DocumentCatalog<TimeEntry, TimeEntryIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeEntryStore"/> class.
    /// </summary>
    public TimeEntryStore(ISession session)
        : base(session)
    {
        CollectionName = TimeTrackerConstants.CollectionName;
    }
}

/// <summary>
/// Manages time categories.
/// </summary>
public sealed class TimeCategoryManager : CatalogManager<TimeCategory>, ITimeCategoryManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeCategoryManager"/> class.
    /// </summary>
    public TimeCategoryManager(TimeCategoryStore store, IEnumerable<ICatalogEntryHandler<TimeCategory>> handlers, ILogger<CatalogManager<TimeCategory>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Manages time stages.
/// </summary>
public sealed class TimeStageManager : CatalogManager<TimeStage>, ITimeStageManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeStageManager"/> class.
    /// </summary>
    public TimeStageManager(TimeStageStore store, IEnumerable<ICatalogEntryHandler<TimeStage>> handlers, ILogger<CatalogManager<TimeStage>> logger)
        : base(store, handlers, logger)
    {
    }
}

internal sealed class TimeCategoryHandler : CatalogEntryHandlerBase<TimeCategory>
{
    private readonly TimeCategoryStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeCategoryHandler(TimeCategoryStore store, IClock clock, IStringLocalizer<TimeCategoryHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<TimeCategory> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TimeCategory> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<TimeCategory> context, CancellationToken cancellationToken = default)
    {
        var category = context.Model;

        if (string.IsNullOrWhiteSpace(category.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(TimeCategory.Name)]));
        }
        else if ((await _store.GetAllAsync(cancellationToken)).Any(other => other.ItemId != category.ItemId && string.Equals(other.Name?.Trim(), category.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            context.Result.Fail(new ValidationResult(S["Another category is called {0}.", category.Name.Trim()], [nameof(TimeCategory.Name)]));
        }

        if (category.Factor is < 0 or > 10)
        {
            context.Result.Fail(new ValidationResult(S["A minute counts from 0 to 10 times."], [nameof(TimeCategory.Factor)]));
        }
    }
}

internal sealed class TimeStageHandler : CatalogEntryHandlerBase<TimeStage>
{
    private readonly TimeStageStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TimeStageHandler(TimeStageStore store, IClock clock, IStringLocalizer<TimeStageHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<TimeStage> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TimeStage> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<TimeStage> context, CancellationToken cancellationToken = default)
    {
        var stage = context.Model;

        if (string.IsNullOrWhiteSpace(stage.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(TimeStage.Name)]));
        }
        else if ((await _store.GetAllAsync(cancellationToken)).Any(other => other.ItemId != stage.ItemId && string.Equals(other.Name?.Trim(), stage.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            context.Result.Fail(new ValidationResult(S["Another stage is called {0}.", stage.Name.Trim()], [nameof(TimeStage.Name)]));
        }
    }
}
