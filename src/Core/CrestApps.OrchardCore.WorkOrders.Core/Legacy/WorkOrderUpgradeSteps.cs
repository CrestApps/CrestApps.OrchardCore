using System.Globalization;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// Gives the open work orders of an upgraded tenant the timers of their policy, a few batches every minute, from where
/// the last run got to, until none is left. It runs in the background, not in the tenant's activation, so a large store
/// or a stopped process never leaves work orders without timers.
/// </summary>
internal sealed class WorkOrderServiceLevelTimersStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "WorkOrders.ServiceLevelTimers";

    private const int BatchesPerRun = 5;

    private readonly WorkOrderServiceLevelUpgrade _upgrade;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderServiceLevelTimersStep"/> class.
    /// </summary>
    /// <param name="upgrade">The upgrade.</param>
    public WorkOrderServiceLevelTimersStep(WorkOrderServiceLevelUpgrade upgrade)
    {
        _upgrade = upgrade;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (_, last, done) = await _upgrade.StartOpenWorkOrdersBatchesAsync(Cursor(context), BatchesPerRun, cancellationToken);

        context.Cursor = last.ToString(CultureInfo.InvariantCulture);

        return done;
    }

    internal static long Cursor(ResumableStepContext context)
        => long.TryParse(context.Cursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cursor) ? cursor : 0;
}

/// <summary>
/// Moves the notes kept on the work orders of an upgraded tenant to their timelines, a few batches every minute, from
/// where the last run got to, until none is left.
/// </summary>
internal sealed class WorkOrderNotesStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "WorkOrders.Notes";

    // Notes are cheap to move, and a record shows the notes not yet moved only after they are; 50 batches of 100 a
    // minute moves a store of 100,000 records in about 20 minutes.
    private const int BatchesPerRun = 50;

    private readonly WorkOrderNotesUpgrade _upgrade;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderNotesStep"/> class.
    /// </summary>
    /// <param name="upgrade">The upgrade.</param>
    public WorkOrderNotesStep(WorkOrderNotesUpgrade upgrade)
    {
        _upgrade = upgrade;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (_, last, done) = await _upgrade.RunBatchesAsync(WorkOrderServiceLevelTimersStep.Cursor(context), BatchesPerRun, cancellationToken);

        context.Cursor = last.ToString(CultureInfo.InvariantCulture);

        return done;
    }
}
