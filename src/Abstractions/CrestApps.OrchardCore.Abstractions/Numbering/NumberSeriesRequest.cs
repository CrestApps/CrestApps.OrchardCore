namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// What an <see cref="INumberSeries"/> needs to know to issue a number, beyond the name of the series.
/// </summary>
public sealed class NumberSeriesRequest
{
    /// <summary>
    /// Gets or sets the date the number is issued on, in the site's time zone, used for the date tokens of the format
    /// and for the year or month a series that starts again counts in. <see langword="null"/> uses today.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Gets or sets a scope that counts on its own within the series, such as a branch or a legal entity.
    /// <see langword="null"/> counts in the series itself.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets a function that returns the highest number a site already uses, read once when the series is new,
    /// so a site that numbered its records another way before carries on after its last number. It is not used by a
    /// series that starts again each year or month.
    /// </summary>
    public Func<CancellationToken, Task<long>> SeedAsync { get; set; }
}
