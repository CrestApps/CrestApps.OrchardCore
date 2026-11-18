using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// Keeps agreements current: expires the ones past their end, tells observers once when one is within its notice of its
/// end, and once a period when its used hours pass 80% and 100% of what the period covers.
/// </summary>
public sealed class AgreementLifecycle
{
    private readonly ISession _session;
    private readonly IAgreementManager _agreements;
    private readonly IAgreementBalanceService _balances;
    private readonly IEnumerable<IAgreementObserver> _observers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementLifecycle"/> class.
    /// </summary>
    public AgreementLifecycle(
        ISession session,
        IAgreementManager agreements,
        IAgreementBalanceService balances,
        IEnumerable<IAgreementObserver> observers,
        IClock clock,
        ILogger<AgreementLifecycle> logger)
    {
        _session = session;
        _agreements = agreements;
        _balances = balances;
        _observers = observers;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs every active agreement.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many agreements changed.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var active = nameof(AgreementStatus.Active);
        var ids = (await _session.QueryIndex<AgreementIndex>(index => index.Status == active, collection: AgreementsConstants.CollectionName).ListAsync(cancellationToken))
            .Select(index => index.ItemId)
            .ToList();
        var changed = 0;

        foreach (var id in ids)
        {
            if (await _agreements.FindByIdAsync(id, cancellationToken) is not { } agreement)
            {
                continue;
            }

            try
            {
                if (await RunAsync(agreement, cancellationToken))
                {
                    changed++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "The agreement {AgreementId} couldn't be brought up to date.", id);
            }
        }

        return changed;
    }

    /// <summary>
    /// Runs one agreement.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it changed.</returns>
    public async Task<bool> RunAsync(Agreement agreement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        if (agreement.Status != AgreementStatus.Active)
        {
            return false;
        }

        var today = AgreementPeriods.DayOf(agreement, _clock.UtcNow);

        if (agreement.EndDate is { } end && end < today)
        {
            agreement.Status = AgreementStatus.Expired;
            await _agreements.UpdateAsync(agreement, null, cancellationToken);

            return true;
        }

        var changed = false;

        if (agreement.EndDate is { } last && agreement.ExpiringNotifiedUtc is null && today.AddDays(agreement.ExpiringNoticeDays) >= last)
        {
            agreement.ExpiringNotifiedUtc = _clock.UtcNow;
            changed = true;

            foreach (var observer in _observers)
            {
                await observer.OnExpiringAsync(agreement, cancellationToken);
            }
        }

        if (today >= agreement.StartDate && await _balances.GetBalanceAsync(agreement, today, cancellationToken) is { CoveredMinutes: > 0 } balance)
        {
            agreement.HoursWarnings ??= new AgreementHoursWarnings();

            // A new period starts its warnings again.
            if (agreement.HoursWarnings.PeriodStart != balance.PeriodStart)
            {
                agreement.HoursWarnings = new AgreementHoursWarnings { PeriodStart = balance.PeriodStart };
                changed = true;
            }

            foreach (var percent in AgreementsConstants.WarningPercents)
            {
                if (balance.UsedShare * 100 >= percent && !agreement.HoursWarnings.Percents.Contains(percent))
                {
                    agreement.HoursWarnings.Percents.Add(percent);
                    changed = true;

                    foreach (var observer in _observers)
                    {
                        await observer.OnHoursUsedAsync(agreement, percent, balance, cancellationToken);
                    }
                }
            }
        }

        if (changed)
        {
            await _agreements.UpdateAsync(agreement, null, cancellationToken);
        }

        return changed;
    }
}
