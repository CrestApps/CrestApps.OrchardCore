using System.Globalization;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Moves the notes kept on the orders of an upgraded tenant to their timelines, a few batches every minute, from where
/// the last run got to, until none is left. It runs in the background, not in the tenant's activation.
/// </summary>
public sealed class OrderNotesStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Orders.Notes";

    // Notes are cheap to move, and a record shows the notes not yet moved only after they are; 50 batches of 100 a
    // minute moves a store of 100,000 records in about 20 minutes.
    private const int BatchesPerRun = 50;

    private readonly OrderNotesUpgrade _upgrade;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderNotesStep"/> class.
    /// </summary>
    /// <param name="upgrade">The upgrade.</param>
    public OrderNotesStep(OrderNotesUpgrade upgrade)
    {
        _upgrade = upgrade;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var after = long.TryParse(context.Cursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cursor) ? cursor : 0;
        var (_, last, done) = await _upgrade.RunBatchesAsync(after, BatchesPerRun, cancellationToken);

        context.Cursor = last.ToString(CultureInfo.InvariantCulture);

        return done;
    }
}
