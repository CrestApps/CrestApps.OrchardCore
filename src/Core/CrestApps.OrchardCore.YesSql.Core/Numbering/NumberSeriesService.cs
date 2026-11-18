using CrestApps.OrchardCore.Numbering;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.YesSql.Core.Numbering;

/// <summary>
/// The <see cref="INumberSeries"/> on the shared <see cref="INumberSequence"/>: the format comes from the site's
/// <see cref="NumberSeriesSettings"/> or from the registered series, and a series that starts again each year or month
/// counts each period on its own.
/// </summary>
internal sealed class NumberSeriesService : INumberSeries
{
    private readonly INumberSequence _numberSequence;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly NumberSeriesOptions _options;

    public NumberSeriesService(
        INumberSequence numberSequence,
        ISiteService siteService,
        ILocalClock localClock,
        IOptions<NumberSeriesOptions> options)
    {
        _numberSequence = numberSequence;
        _siteService = siteService;
        _localClock = localClock;
        _options = options.Value;
    }

    public async Task<NumberSeriesFormat> GetFormatAsync(string series, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(series);

        var settings = await _siteService.GetSettingsAsync<NumberSeriesSettings>();

        // A stored format that can no longer be used never stops a number from being issued: the registered one is used.
        if (settings?.Formats is not null &&
            settings.Formats.TryGetValue(series, out var chosen) &&
            chosen is not null &&
            chosen.Validate() == NumberSeriesFormatError.None)
        {
            return chosen;
        }

        return _options.Series.TryGetValue(series, out var registered) && registered.DefaultFormat is not null
            ? registered.DefaultFormat
            : new NumberSeriesFormat();
    }

    public async Task<SeriesNumber> NextAsync(string series, NumberSeriesRequest request = null, CancellationToken cancellationToken = default)
    {
        var format = await GetFormatAsync(series, cancellationToken);
        var date = request?.Date ?? DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var period = format.GetPeriod(date);

        var key = series;

        if (!string.IsNullOrEmpty(request?.Scope))
        {
            key += ":" + request.Scope;
        }

        if (period is not null)
        {
            key += "@" + period;
        }

        var options = new NumberSequenceOptions
        {
            FirstNumber = Math.Max(1, format.FirstNumber),

            // The numbers in use before the series existed belong to the series as a whole, not to a year or a month.
            SeedAsync = period is null ? request?.SeedAsync : null,
        };

        var sequence = await _numberSequence.NextAsync(key, options, cancellationToken);

        return new SeriesNumber(sequence, format.Format(sequence, date));
    }
}
