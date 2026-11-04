namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// Where a series of an <see cref="INumberSequence"/> starts. The options matter only the first time a series is used.
/// </summary>
public sealed class NumberSequenceOptions
{
    /// <summary>
    /// Gets or sets the first number of a new series.
    /// </summary>
    public long FirstNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets a function that returns the highest number a site already uses, read once when the series is new,
    /// so a site that numbered its records another way before carries on after its last number. <see langword="null"/>
    /// when there are no earlier numbers.
    /// </summary>
    public Func<CancellationToken, Task<long>> SeedAsync { get; set; }
}
