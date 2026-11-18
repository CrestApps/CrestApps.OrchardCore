using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Agreements.Reports;

/// <summary>
/// What the agreement reports read: the agreements with covered hours (the one the filter names, or every active one),
/// each with its balance for the period that holds the report's last day, and the names of their accounts.
/// </summary>
internal sealed class AgreementReportData
{
    /// <summary>
    /// The filter key of the agreement the reports are about.
    /// </summary>
    public const string AgreementFilterKey = "AgreementId";

    private readonly ISession _session;
    private readonly IAgreementManager _agreements;
    private readonly IAgreementBalanceService _balances;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;

    public AgreementReportData(ISession session, IAgreementManager agreements, IAgreementBalanceService balances, IContentManager contentManager, IClock clock)
    {
        _session = session;
        _agreements = agreements;
        _balances = balances;
        _contentManager = contentManager;
        _clock = clock;
    }

    /// <summary>
    /// Reads the agreements the report is about, with their balance as of the report's last day (today at the latest).
    /// </summary>
    /// <param name="context">The report's context.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The agreements with covered hours, by number.</returns>
    public async Task<IReadOnlyList<AgreementReportItem>> ReadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var agreements = new List<Agreement>();

        if (context.Filter.GetOrDefault<string>(AgreementFilterKey) is { Length: > 0 } agreementId)
        {
            if (await _agreements.FindByIdAsync(agreementId, cancellationToken) is { } chosen)
            {
                agreements.Add(chosen);
            }
        }
        else
        {
            foreach (var id in await ListActiveIdsAsync(cancellationToken))
            {
                if (await _agreements.FindByIdAsync(id, cancellationToken) is { } agreement)
                {
                    agreements.Add(agreement);
                }
            }
        }

        var now = _clock.UtcNow;
        var lastUtc = context.Filter.GetDateRange().ToUtc is { } to && to < now ? to : now;
        var items = new List<AgreementReportItem>();

        foreach (var agreement in agreements.Where(agreement => agreement.Hours is { Kind: not AgreementHoursKind.None }))
        {
            var through = AgreementPeriods.DayOf(agreement, lastUtc);

            if (through < agreement.StartDate)
            {
                continue;
            }

            if (await _balances.GetBalanceAsync(agreement, agreement.EndDate is { } end && end < through ? end : through, cancellationToken) is { } balance)
            {
                items.Add(new AgreementReportItem(agreement, balance, through));
            }
        }

        return [.. items.OrderBy(item => item.Agreement.Number, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Lists the agreements with covered hours a reader can choose, by number.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Each agreement's id and its number and name.</returns>
    public async Task<IReadOnlyList<(string Id, string Text)>> ListChoicesAsync(CancellationToken cancellationToken)
    {
        var choices = new List<(string Id, string Text)>();

        foreach (var index in await _session.QueryIndex<AgreementIndex>(collection: AgreementsConstants.CollectionName).OrderBy(index => index.Number).ListAsync(cancellationToken))
        {
            if (await _agreements.FindByIdAsync(index.ItemId, cancellationToken) is { Hours.Kind: not AgreementHoursKind.None } agreement)
            {
                choices.Add((agreement.ItemId, string.IsNullOrEmpty(agreement.Number) ? agreement.Name : agreement.Number + " " + agreement.Name));
            }
        }

        return choices;
    }

    /// <summary>
    /// Gets the names of the accounts.
    /// </summary>
    /// <param name="accountIds">The accounts' content item ids.</param>
    /// <returns>Each account's name by its id.</returns>
    public async Task<IReadOnlyDictionary<string, string>> GetAccountNamesAsync(IEnumerable<string> accountIds)
    {
        var ids = accountIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return (await _contentManager.GetAsync(ids, VersionOptions.Latest))
            .GroupBy(item => item.ContentItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayText, StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<string>> ListActiveIdsAsync(CancellationToken cancellationToken)
    {
        var active = nameof(AgreementStatus.Active);

        return [.. (await _session.QueryIndex<AgreementIndex>(index => index.Status == active, collection: AgreementsConstants.CollectionName).ListAsync(cancellationToken)).Select(index => index.ItemId)];
    }
}

/// <summary>
/// An agreement in a report, with its balance for the period that holds the report's last day.
/// </summary>
/// <param name="Agreement">The agreement.</param>
/// <param name="Balance">Its balance.</param>
/// <param name="Through">The report's last day in the agreement's time zone (today at the latest).</param>
internal sealed record AgreementReportItem(Agreement Agreement, AgreementBalance Balance, DateOnly Through)
{
    /// <summary>
    /// Gets the agreement's number and name.
    /// </summary>
    public string Title => string.IsNullOrEmpty(Agreement.Number) ? Agreement.Name : Agreement.Number + " " + Agreement.Name;
}
