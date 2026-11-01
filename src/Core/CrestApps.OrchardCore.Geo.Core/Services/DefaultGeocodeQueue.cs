using CrestApps.Core.Support;
using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Geo.Core.Services;

/// <summary>
/// Geocodes addresses in the background: one request per record, retried with growing waits, written back through
/// the record's <see cref="IGeocodeTarget"/>.
/// </summary>
public sealed class DefaultGeocodeQueue : IGeocodeQueue
{
    /// <summary>
    /// How many failed attempts a request gets before it is given up.
    /// </summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// How long a request waits when no geocoding provider is set up.
    /// </summary>
    public static readonly TimeSpan NoProviderWait = TimeSpan.FromHours(1);

    private static readonly TimeSpan[] _retryWaits = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)];

    private readonly ISession _session;
    private readonly GeocodeRequestStore _store;
    private readonly IGeoService _geoService;
    private readonly IEnumerable<IGeocodeTarget> _targets;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultGeocodeQueue"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="store">The request store.</param>
    /// <param name="geoService">The geo service.</param>
    /// <param name="targets">The targets.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultGeocodeQueue(ISession session, GeocodeRequestStore store, IGeoService geoService, IEnumerable<IGeocodeTarget> targets, IClock clock, ILogger<DefaultGeocodeQueue> logger)
    {
        _session = session;
        _store = store;
        _geoService = geoService;
        _targets = targets;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gives the wait after a number of failed attempts.
    /// </summary>
    /// <param name="attempts">The failed attempts so far.</param>
    /// <returns>The wait.</returns>
    public static TimeSpan RetryWait(int attempts) => _retryWaits[Math.Clamp(attempts - 1, 0, _retryWaits.Length - 1)];

    /// <inheritdoc/>
    public async Task EnqueueAsync(string targetType, string targetId, GeocodeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetType);
        ArgumentException.ThrowIfNullOrEmpty(targetId);
        ArgumentNullException.ThrowIfNull(query);

        var request = await _session.Query<GeocodeRequest, GeocodeRequestIndex>(index => index.TargetType == targetType && index.TargetId == targetId, collection: GeoConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);
        var now = _clock.UtcNow;
        var isNew = request is null;

        if (isNew)
        {
            request = new GeocodeRequest { ItemId = IdGenerator.GenerateId(), TargetType = targetType, TargetId = targetId, CreatedUtc = now };
        }

        request.Query = query;
        request.Status = GeocodeRequestStatus.Pending;
        request.Attempts = 0;
        request.NextAttemptUtc = now;
        request.LastError = null;
        request.Result = null;
        request.Source = null;
        request.CompletedUtc = null;

        if (isNew)
        {
            await _store.CreateAsync(request, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(request, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<int> ProcessDueAsync(int max = 50, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var pending = GeocodeRequestStatus.Pending.ToString();
        var due = await _session.Query<GeocodeRequest, GeocodeRequestIndex>(index => index.Status == pending && index.NextAttemptUtc <= now, collection: GeoConstants.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(max)
            .ListAsync(cancellationToken);
        var count = 0;

        foreach (var request in due)
        {
            await ProcessAsync(request, now, cancellationToken);
            await _store.UpdateAsync(request, cancellationToken);
            count++;
        }

        return count;
    }

    private async Task ProcessAsync(GeocodeRequest request, DateTime now, CancellationToken cancellationToken)
    {
        var target = _targets.FirstOrDefault(item => string.Equals(item.TargetType, request.TargetType, StringComparison.Ordinal));

        if (target is null)
        {
            Fail(request, now, $"Nothing writes back a {request.TargetType}.");

            return;
        }

        GeocodeResponse response;

        try
        {
            response = await _geoService.GeocodeAsync(request.Query, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            request.Attempts++;
            request.LastError = ex.Message;

            _logger.LogWarning(ex, "Geocoding {TargetType} {TargetId} failed (attempt {Attempt}).", request.TargetType.SanitizeLogValue(), request.TargetId.SanitizeLogValue(), request.Attempts);

            if (request.Attempts >= MaxAttempts)
            {
                Fail(request, now, ex.Message);
            }
            else
            {
                request.NextAttemptUtc = now + RetryWait(request.Attempts);
            }

            return;
        }

        if (response.Provider is null)
        {
            request.LastError = "No geocoding provider is set up.";
            request.NextAttemptUtc = now + NoProviderWait;

            return;
        }

        var best = response.Candidates.Count > 0 ? response.Candidates[0] : null;

        if (best is null)
        {
            Fail(request, now, "No place matches the address.");

            return;
        }

        if (!await target.ApplyAsync(request.TargetId, best, response.Provider, cancellationToken))
        {
            Fail(request, now, "The record is gone.");

            return;
        }

        request.Status = GeocodeRequestStatus.Done;
        request.Result = best.Point;
        request.Source = response.Provider;
        request.LastError = null;
        request.CompletedUtc = now;
    }

    private static void Fail(GeocodeRequest request, DateTime now, string error)
    {
        request.Status = GeocodeRequestStatus.Failed;
        request.LastError = error;
        request.CompletedUtc = now;
    }
}
