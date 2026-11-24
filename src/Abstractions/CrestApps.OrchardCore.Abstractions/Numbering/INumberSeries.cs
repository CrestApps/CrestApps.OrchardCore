namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// Issues the formatted numbers of a registered series, such as invoice numbers. The numbers come from the
/// <see cref="INumberSequence"/>, so they are issued under a lock and saved in the caller's unit of work, and the format
/// is the one the site chose for the series or the one the series was registered with.
/// </summary>
public interface INumberSeries
{
    /// <summary>
    /// Gets the format a series uses now.
    /// </summary>
    /// <param name="series">The name of the series.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The format the site chose, the registered one, or one that writes the bare number.</returns>
    Task<NumberSeriesFormat> GetFormatAsync(string series, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the next number of a series.
    /// </summary>
    /// <param name="series">The name of the series.</param>
    /// <param name="request">The date, scope and seed, or <see langword="null"/> for today, no scope and no seed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number and its text.</returns>
    Task<SeriesNumber> NextAsync(string series, NumberSeriesRequest request = null, CancellationToken cancellationToken = default);
}
