namespace CrestApps.OrchardCore.YesSql.Core.Retention;

/// <summary>
/// How much the platform retention task removes in one cycle, from the <c>CrestApps:Retention</c> section.
/// </summary>
public sealed class RetentionOptions
{
    /// <summary>
    /// The default number of records removed and committed together.
    /// </summary>
    public const int DefaultPurgeBatchSize = 200;

    /// <summary>
    /// The default number of batches one policy may run in one cycle.
    /// </summary>
    public const int DefaultMaxPurgeBatchesPerCycle = 50;

    /// <summary>
    /// Gets or sets the number of records removed and committed together.
    /// </summary>
    public int PurgeBatchSize { get; set; } = DefaultPurgeBatchSize;

    /// <summary>
    /// Gets or sets the number of batches one policy may run in one cycle.
    /// </summary>
    public int MaxPurgeBatchesPerCycle { get; set; } = DefaultMaxPurgeBatchesPerCycle;
}
