using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// Adds up the time every usage provider reports under an agreement in a period, each entry rounded as the agreement
/// says, against the hours the period covers.
/// </summary>
public sealed class AgreementBalanceService : IAgreementBalanceService
{
    private readonly ISession _session;
    private readonly AgreementStore _store;
    private readonly IEnumerable<IAgreementUsageProvider> _providers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementBalanceService"/> class.
    /// </summary>
    public AgreementBalanceService(ISession session, AgreementStore store, IEnumerable<IAgreementUsageProvider> providers, IClock clock, ILogger<AgreementBalanceService> logger)
    {
        _session = session;
        _store = store;
        _providers = providers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AgreementBalance> GetBalanceAsync(Agreement agreement, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        var hours = agreement.Hours;

        if (hours is null || hours.Kind == AgreementHoursKind.None)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var (start, end) = AgreementPeriods.PeriodOf(agreement, date ?? AgreementPeriods.DayOf(agreement, now));
        var fromUtc = AgreementPeriods.StartOfDayUtc(agreement, start);

        // A block with no end runs until now: nothing is spent in the future.
        var toUtc = end is { } last ? AgreementPeriods.StartOfDayUtc(agreement, last.AddDays(1)) : now.AddMinutes(1);
        var entries = new List<(AgreementUsage, double)>();

        foreach (var provider in _providers)
        {
            IReadOnlyList<AgreementUsage> usage;

            try
            {
                usage = await provider.ListAsync(agreement, fromUtc, toUtc, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "The agreement usage provider {Provider} failed on agreement {AgreementId}.", provider.Name, agreement.ItemId);

                continue;
            }

            foreach (var entry in usage ?? [])
            {
                if (entry.StartUtc >= fromUtc && entry.StartUtc < toUtc && entry.Minutes > 0)
                {
                    entry.Source ??= provider.Name;
                    entries.Add((entry, AgreementPeriods.Round(entry.Minutes, hours.RoundingMinutes)));
                }
            }
        }

        return new AgreementBalance
        {
            PeriodStart = start,
            PeriodEnd = end,
            CoveredMinutes = (double)(hours.Hours * 60),
            UsedMinutes = entries.Sum(entry => entry.Item2),
            Entries = [.. entries.OrderBy(entry => entry.Item1.StartUtc)],
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Agreement>> ListInForceAsync(string accountId, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            return [];
        }

        var active = nameof(AgreementStatus.Active);
        var ids = (await _session.QueryIndex<AgreementIndex>(index => index.AccountId == accountId && index.Status == active, collection: AgreementsConstants.CollectionName).ListAsync(cancellationToken))
            .Select(index => index.ItemId)
            .ToList();
        var now = _clock.UtcNow;
        var agreements = new List<Agreement>();

        foreach (var id in ids)
        {
            if (await _store.FindByIdAsync(id, cancellationToken) is { } agreement && agreement.IsInForceOn(date ?? AgreementPeriods.DayOf(agreement, now)))
            {
                agreements.Add(agreement);
            }
        }

        return [.. agreements.OrderBy(agreement => agreement.StartDate).ThenBy(agreement => agreement.Number, StringComparer.Ordinal)];
    }
}
