using System.Globalization;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// How each account is doing: the tickets it opened and had solved in the period, the ones open now, the time worked on
/// them (with Time Tracker), how many service-level targets were met, how much of its agreements' hours are used (with
/// Agreements, for those who may view agreements) and how satisfied its requesters were.
/// </summary>
internal sealed class TicketAccountHealthReport : IReport
{
    // A share of met targets, and of agreement hours, from which a cell is colored.
    private const double GoodShare = 0.9;
    private const double WarningShare = 0.8;
    private static readonly ReportStyle _good = ReportStyle.Create("#166534", "#DCFCE7");
    private static readonly ReportStyle _warning = ReportStyle.Create("#92400E", "#FEF3C7");
    private static readonly ReportStyle _bad = ReportStyle.Create("#991B1B", "#FEE2E2", bold: true);

    private readonly TicketReportData _data;
    private readonly IServiceProvider _services;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public TicketAccountHealthReport(
        TicketReportData data,
        IServiceProvider services,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<TicketAccountHealthReport> stringLocalizer)
    {
        _data = data;
        _services = services;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public static string Name => "tickets-account-health";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketAccountHealthReport>("Account Health");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketAccountHealthReport>("For each account: tickets opened, solved and open now, time worked, service-level targets met, agreement hours used and requester satisfaction.");

    public static string Category => ReportsConstants.Categories.Executive;

    public static Permission Permission => TicketsPermissions.ViewTicketReports;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _data.GetScopeAsync(context, cancellationToken) is not { } scope)
        {
            return TicketReportSections.NothingToShow(S);
        }

        var range = context.Filter.GetDateRange();
        var from = range.FromUtc ?? DateTime.MinValue;
        var to = range.ToUtc ?? DateTime.MaxValue;
        var opened = await _data.ReadAsync(scope, index => index.AccountId != null && index.CreatedUtc >= from && index.CreatedUtc <= to, index => index.CreatedUtc, cancellationToken);
        var solved = await _data.ReadAsync(scope, index => index.AccountId != null && index.SolvedUtc != null && index.SolvedUtc >= from && index.SolvedUtc <= to, index => index.CreatedUtc, cancellationToken);
        var open = await _data.ReadAsync(
            scope,
            index => index.AccountId != null && (index.StatusCategory == TicketStatusCategory.New || index.StatusCategory == TicketStatusCategory.Open || index.StatusCategory == TicketStatusCategory.Pending || index.StatusCategory == TicketStatusCategory.OnHold),
            index => index.CreatedUtc,
            cancellationToken);

        // The targets that ended in the period, by the account of their ticket.
        const string kind = TicketsConstants.RecordKind;
        var timers = await _data.ReadTimersAsync(scope, rows => rows.Where(index => index.SubjectKind == kind && index.EndedUtc != null && index.EndedUtc >= from && index.EndedUtc <= to), cancellationToken);
        var timerTickets = await _data.ReadByIdsAsync(scope, timers.Select(timer => timer.Subject.Id), cancellationToken);
        var targets = timers
            .Select(timer => (Account: timerTickets.TryGetValue(timer.Subject.Id ?? string.Empty, out var ticket) ? ticket.AccountId : null, Met: !timer.IsBreached))
            .Where(target => !string.IsNullOrEmpty(target.Account))
            .ToLookup(target => target.Account, StringComparer.Ordinal);

        var time = _services.GetService<TicketTimeReportData>() is { } timeData
            ? (await timeData.ReadWorkAsync(scope, range.FromUtc, range.ToUtc, cancellationToken))
                .Where(work => !string.IsNullOrEmpty(work.Ticket.AccountId))
                .ToLookup(work => work.Ticket.AccountId, StringComparer.Ordinal)
            : null;

        var accountIds = opened.Concat(solved).Concat(open).Select(ticket => ticket.AccountId)
            .Concat(targets.Select(group => group.Key))
            .Concat(time?.Select(group => group.Key) ?? [])
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var agreements = await ReadAgreementSharesAsync(context, accountIds, cancellationToken);
        var names = await _data.GetAccountNamesAsync(accountIds, cancellationToken);
        var openedBy = opened.ToLookup(ticket => ticket.AccountId, StringComparer.Ordinal);
        var solvedBy = solved.ToLookup(ticket => ticket.AccountId, StringComparer.Ordinal);
        var openBy = open.ToLookup(ticket => ticket.AccountId, StringComparer.Ordinal);
        var ratings = solved.Where(ticket => ticket.SatisfactionScore is not null).ToList();
        var met = targets.Sum(group => group.Count(target => target.Met));
        var ended = targets.Sum(group => group.Count());

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Accounts"].Value, ReportFormat.Number(accountIds.Count)),
                new ReportMetric(S["Tickets opened"].Value, ReportFormat.Number(opened.Count)),
                new ReportMetric(S["Tickets solved"].Value, ReportFormat.Number(solved.Count)),
                new ReportMetric(S["Open now"].Value, ReportFormat.Number(open.Count)),
                new ReportMetric(S["Targets met"].Value, TicketReportData.Rate(met, ended)),
                new ReportMetric(S["Satisfaction"].Value, Satisfaction(ratings), S["The average score, from 1 to 5, of the tickets solved in the period that their requester rated."].Value),
            ]));

        var rows = accountIds
            .Select(id =>
            {
                var accountTargets = targets[id].ToList();
                var accountRatings = solvedBy[id].Where(ticket => ticket.SatisfactionScore is not null).ToList();

                return (
                    Name: names.GetValueOrDefault(id) ?? id,
                    Opened: openedBy[id].Count(),
                    Solved: solvedBy[id].Count(),
                    Open: openBy[id].Count(),
                    Minutes: time?[id].Sum(work => work.Minutes),
                    Met: accountTargets.Count(target => target.Met),
                    Ended: accountTargets.Count,
                    Agreement: agreements is not null && agreements.TryGetValue(id, out var share) ? share : (double?)null,
                    Ratings: accountRatings);
            })
            .OrderByDescending(row => row.Open)
            .ThenByDescending(row => row.Opened)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(row =>
            {
                var cells = new List<string>
                {
                    row.Name,
                    ReportFormat.Number(row.Opened),
                    ReportFormat.Number(row.Solved),
                    ReportFormat.Number(row.Open),
                };

                if (time is not null)
                {
                    cells.Add(ReportFormat.Duration(Math.Round(row.Minutes ?? 0) * 60));
                }

                var metIndex = cells.Count;
                cells.Add(TicketReportData.Rate(row.Met, row.Ended));
                var agreementIndex = cells.Count;

                if (agreements is not null)
                {
                    cells.Add(row.Agreement is { } share ? ReportFormat.Percent(share) : "-");
                }

                cells.Add(Satisfaction(row.Ratings));

                var reportRow = new ReportRow(cells);

                if (row.Ended > 0)
                {
                    var rate = (double)row.Met / row.Ended;
                    reportRow = reportRow.WithCellStyle(metIndex, rate >= GoodShare ? _good : rate >= WarningShare ? _warning : _bad);
                }

                if (agreements is not null && row.Agreement is { } used && used >= WarningShare)
                {
                    reportRow = reportRow.WithCellStyle(agreementIndex, used >= 1 ? _bad : _warning);
                }

                return reportRow;
            })
            .ToList();

        var columns = new List<ReportColumn>
        {
            new(S["Account"].Value),
            new(S["Opened"].Value, ReportColumnAlign.End),
            new(S["Solved"].Value, ReportColumnAlign.End),
            new(S["Open now"].Value, ReportColumnAlign.End),
        };

        if (time is not null)
        {
            columns.Add(new ReportColumn(S["Time worked"].Value, ReportColumnAlign.End));
        }

        columns.Add(new ReportColumn(S["Targets met"].Value, ReportColumnAlign.End));

        if (agreements is not null)
        {
            columns.Add(new ReportColumn(S["Agreement hours used"].Value, ReportColumnAlign.End));
        }

        columns.Add(new ReportColumn(S["Satisfaction"].Value, ReportColumnAlign.End));

        var table = ReportSection.ForTable(S["By account"].Value, columns, rows);
        table.Description = agreements is null
            ? S["The accounts with the most open tickets first."].Value
            : S["The accounts with the most open tickets first. Agreement hours used is the share of the hours of the account's agreements in force today used in their current period."].Value;
        table.EmptyMessage = S["No account had tickets, time or service-level targets in these days."].Value;

        return document.Add(table);
    }

    // The share of the hours of each account's agreements in force today used in their period; null when agreements are
    // off or the reader may not view them.
    private async Task<Dictionary<string, double>> ReadAgreementSharesAsync(ReportContext context, IReadOnlyList<string> accountIds, CancellationToken cancellationToken)
    {
        if (_services.GetService<IAgreementBalanceService>() is not { } balances ||
            (context.User ?? _httpContextAccessor.HttpContext?.User) is not { } user ||
            !await _authorizationService.AuthorizeAsync(user, AgreementsPermissions.ViewAgreements))
        {
            return null;
        }

        var shares = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var accountId in accountIds)
        {
            var covered = 0d;
            var used = 0d;

            foreach (var agreement in await balances.ListInForceAsync(accountId, cancellationToken: cancellationToken))
            {
                if (await balances.GetBalanceAsync(agreement, cancellationToken: cancellationToken) is { CoveredMinutes: > 0 } balance)
                {
                    covered += balance.CoveredMinutes;
                    used += balance.UsedMinutes;
                }
            }

            if (covered > 0)
            {
                shares[accountId] = used / covered;
            }
        }

        return shares;
    }

    private static string Satisfaction(List<TicketFacts> rated)
        => rated.Count == 0
            ? "-"
            : string.Create(CultureInfo.InvariantCulture, $"{rated.Average(ticket => ticket.SatisfactionScore.Value):0.0} ({rated.Count})");
}
