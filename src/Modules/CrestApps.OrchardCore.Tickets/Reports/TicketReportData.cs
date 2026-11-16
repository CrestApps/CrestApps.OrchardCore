using System.Globalization;
using System.Linq.Expressions;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Reports.DataSources;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// What a ticket report needs of a ticket, read once from the ticket.
/// </summary>
internal sealed record TicketFacts(
    string Id,
    string ContentType,
    string DeskId,
    string TeamId,
    string AssigneeId,
    string PriorityId,
    TicketStatusCategory Category,
    string Channel,
    DateTime CreatedUtc,
    DateTime? FirstResponseUtc,
    DateTime? SolvedUtc,
    DateTime? NextDueUtc,
    int ReplyCount,
    int AgentReplyCount,
    int ReopenCount,
    int EscalationLevel)
{
    /// <summary>
    /// Gets the ticket's number.
    /// </summary>
    public string Number { get; init; }

    /// <summary>
    /// Gets the account the ticket is for.
    /// </summary>
    public string AccountId { get; init; }

    /// <summary>
    /// Gets the requester's satisfaction score, from 1 to 5; <see langword="null"/> when they didn't rate the ticket.
    /// </summary>
    public int? SatisfactionScore { get; init; }

    public bool IsOpen => Category is TicketStatusCategory.New or TicketStatusCategory.Open or TicketStatusCategory.Pending or TicketStatusCategory.OnHold;

    public double? FirstResponseMinutes => FirstResponseUtc is { } at && at >= CreatedUtc ? (at - CreatedUtc).TotalMinutes : null;

    public double? ResolutionMinutes => SolvedUtc is { } at && at >= CreatedUtc ? (at - CreatedUtc).TotalMinutes : null;

    /// <summary>
    /// Gets whether the ticket was solved with exactly one public reply from an agent (automated replies and the
    /// requester's own don't count) and never reopened.
    /// </summary>
    public bool SolvedOnFirstContact => SolvedUtc is not null && ReopenCount == 0 && AgentReplyCount == 1;
}

/// <summary>
/// Reads tickets for the ticket reports, for the reader and within their ticket scope, and the names and time zone the
/// reports show them with.
/// </summary>
internal sealed class TicketReportData
{
    // How many index rows are read at a time.
    private const int BatchSize = 1000;

    // How many tickets are read by id at a time.
    private const int IdBatchSize = 500;

    private readonly ISession _session;
    private readonly TicketReportAccess _access;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly IServiceProvider _services;

    public TicketReportData(
        ISession session,
        TicketReportAccess access,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        IClock clock,
        IServiceProvider services)
    {
        _session = session;
        _access = access;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
        _clock = clock;
        _services = services;
    }

    public DateTime UtcNow => _clock.UtcNow;

    /// <summary>
    /// Gets the reader's ticket scope, or <see langword="null"/> when they may see no ticket. The reader is the one the
    /// report runs for (the subscriber of a scheduled e-mail too), or the request's user.
    /// </summary>
    public Task<TicketScopedQuery> GetScopeAsync(ReportContext context, CancellationToken cancellationToken)
        => _access.GetScopeAsync(context?.User ?? _httpContextAccessor.HttpContext?.User, cancellationToken);

    public Task<TicketReportNames> GetNamesAsync(CancellationToken cancellationToken)
        => _access.GetNamesAsync(cancellationToken);

    /// <summary>
    /// Reads the tickets in the scope that match a condition, from the ticket index only (no ticket is loaded), a batch at
    /// a time. Merged tickets are left out.
    /// </summary>
    public async Task<List<TicketFacts>> ReadAsync(
        TicketScopedQuery scope,
        Expression<Func<TicketIndex, bool>> condition,
        Expression<Func<TicketIndex, DateTime>> order,
        CancellationToken cancellationToken)
    {
        var predicate = TicketScopeFilter.ToPredicate(scope);
        var facts = new List<TicketFacts>();
        var lastId = 0L;

        while (true)
        {
            var afterId = lastId;
            var rows = (await _session.QueryIndex<TicketIndex>(predicate)
                .Where(condition)
                .Where(index => index.MergedIntoId == null && index.Id > afterId)
                .OrderBy(index => index.Id)
                .Take(BatchSize)
                .ListAsync(cancellationToken)).ToList();

            facts.AddRange(rows.Select(row => new TicketFacts(
                row.ContentItemId,
                row.ContentType,
                row.DeskId,
                row.TeamId,
                row.AssigneeId,
                row.PriorityId,
                row.StatusCategory,
                row.Channel,
                row.CreatedUtc,
                row.FirstResponseUtc,
                row.SolvedUtc,
                row.NextDueUtc,
                row.ReplyCount,
                row.AgentReplyCount,
                row.ReopenCount,
                row.EscalationLevel)
            {
                Number = row.Number,
                AccountId = row.AccountId,
                SatisfactionScore = row.SatisfactionScore,
            }));

            if (rows.Count < BatchSize)
            {
                return facts;
            }

            lastId = rows[^1].Id;
        }
    }

    /// <summary>
    /// Reads the tickets in the scope with the given ids, by id, a batch of ids at a time. Merged tickets are left out.
    /// </summary>
    public async Task<Dictionary<string, TicketFacts>> ReadByIdsAsync(TicketScopedQuery scope, IEnumerable<string> ticketIds, CancellationToken cancellationToken)
    {
        var tickets = new Dictionary<string, TicketFacts>(StringComparer.Ordinal);

        foreach (var batch in ticketIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Chunk(IdBatchSize))
        {
            foreach (var ticket in await ReadAsync(scope, index => index.ContentItemId.IsIn(batch), index => index.CreatedUtc, cancellationToken))
            {
                tickets[ticket.Id] = ticket;
            }
        }

        return tickets;
    }

    /// <summary>
    /// Gets the names of accounts by id; an account that can't be read is left out.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAccountNamesAsync(IEnumerable<string> accountIds, CancellationToken cancellationToken)
    {
        var ids = accountIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (ids.Count == 0 || _services.GetService<IContentManager>() is not { } contentManager)
        {
            return names;
        }

        foreach (var account in await contentManager.GetAsync(ids, VersionOptions.Latest))
        {
            if (!string.IsNullOrWhiteSpace(account?.DisplayText))
            {
                names[account.ContentItemId] = account.DisplayText;
            }
        }

        return names;
    }

    /// <summary>
    /// Reads the service-level timers that match a filter, keeping only the ones of tickets the reader may see.
    /// </summary>
    public async Task<List<ServiceLevelTimer>> ReadTimersAsync(TicketScopedQuery scope, Func<IQueryIndex<ServiceLevelTimerIndex>, IQueryIndex<ServiceLevelTimerIndex>> filter, CancellationToken cancellationToken)
    {
        var timers = new List<ServiceLevelTimer>();
        var all = ReportIndexPages.ReadAsync<ServiceLevelTimer, ServiceLevelTimerIndex>(_session, filter, index => index.StartedUtc, ServiceLevelsConstants.CollectionName, cancellationToken);

        await foreach (var timer in _access.KeepVisibleAsync(scope, all, timer => timer.Subject.Id, cancellationToken))
        {
            timers.Add(timer);
        }

        return timers;
    }

    /// <summary>
    /// Reads the backlog snapshots of the reader's desks taken in a period, keeping only the groups in their scope.
    /// </summary>
    public async Task<List<(string Day, TicketBacklogGroup Group)>> ReadBacklogAsync(TicketScopedQuery scope, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var desks = scope.AllDeskIds.Concat(scope.TeamDeskIds).Concat(scope.AssignedDeskIds).Distinct(StringComparer.Ordinal).ToArray();
        var from = fromUtc ?? DateTime.MinValue;
        var to = toUtc ?? DateTime.MaxValue;
        var rows = new List<(string, TicketBacklogGroup)>();

        await foreach (var day in ReportIndexPages.ReadAsync<TicketBacklogDay, TicketBacklogIndex>(
            _session,
            index => index.Where(row => row.DeskId.IsIn(desks) && row.TakenUtc >= from && row.TakenUtc <= to),
            index => index.TakenUtc,
            null,
            cancellationToken))
        {
            rows.AddRange(day.Groups
                .Where(group => TicketScopeFilter.Allows(scope, day.DeskId, group.TeamId, group.AssigneeId))
                .Select(group => (day.Day, group)));
        }

        return rows;
    }

    /// <summary>
    /// Gets the names of users by id.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();

        if (ids.Count == 0 || _services.GetService<IPartyDirectory>() is not { } parties)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var names = await parties.GetNamesAsync(ids.Select(id => new PartyRef(PartyKinds.User, id)), cancellationToken);

        return names.ToDictionary(pair => pair.Key.Id, pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the names of teams by id.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetTeamNamesAsync(CancellationToken cancellationToken)
    {
        if (_services.GetService<ITeamManager>() is not { } teams)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return (await teams.ListTreeAsync(cancellationToken)).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the reader's time zone, which days are counted in.
    /// </summary>
    public async Task<TimeZoneInfo> GetTimeZoneAsync()
    {
        var zone = await _localClock.GetLocalTimeZoneAsync();

        return zone?.TimeZoneId is { Length: > 0 } id && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var info) ? info : TimeZoneInfo.Utc;
    }

    public static string Day(DateTime utc, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static double? Median(IEnumerable<double?> values)
    {
        var sorted = values.Where(value => value.HasValue).Select(value => value.Value).Order().ToList();

        if (sorted.Count == 0)
        {
            return null;
        }

        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    public static double? Average(IEnumerable<double?> values)
    {
        var known = values.Where(value => value.HasValue).Select(value => value.Value).ToList();

        return known.Count == 0 ? null : known.Average();
    }

    public static string Minutes(double? minutes)
        => minutes is { } value ? ReportFormat.Duration(value * 60) : "-";

    public static string Rate(int part, int whole)
        => whole == 0 ? "-" : ReportFormat.Percent((double)part / whole);
}
