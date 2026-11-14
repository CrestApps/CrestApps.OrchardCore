using System.Globalization;
using System.Linq.Expressions;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Reports.Models;
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
    int ReopenCount,
    int EscalationLevel)
{
    public bool IsOpen => Category is TicketStatusCategory.New or TicketStatusCategory.Open or TicketStatusCategory.Pending or TicketStatusCategory.OnHold;

    public double? FirstResponseMinutes => FirstResponseUtc is { } at && at >= CreatedUtc ? (at - CreatedUtc).TotalMinutes : null;

    public double? ResolutionMinutes => SolvedUtc is { } at && at >= CreatedUtc ? (at - CreatedUtc).TotalMinutes : null;

    /// <summary>
    /// Gets whether the ticket was solved after a single reply and never reopened.
    /// </summary>
    public bool SolvedOnFirstContact => SolvedUtc is not null && ReopenCount == 0 && ReplyCount <= 2;
}

/// <summary>
/// Reads tickets for the ticket reports, for the reader and within their ticket scope, and the names and time zone the
/// reports show them with.
/// </summary>
internal sealed class TicketReportData
{
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
    /// Gets the reader's ticket scope, or <see langword="null"/> when they may see no ticket.
    /// </summary>
    public Task<TicketScopedQuery> GetScopeAsync(CancellationToken cancellationToken)
        => _access.GetScopeAsync(_httpContextAccessor.HttpContext?.User, cancellationToken);

    public Task<TicketReportNames> GetNamesAsync(CancellationToken cancellationToken)
        => _access.GetNamesAsync(cancellationToken);

    /// <summary>
    /// Reads the tickets in the scope that match a condition, a page at a time, ordered by a date.
    /// </summary>
    public async Task<List<TicketFacts>> ReadAsync(
        TicketScopedQuery scope,
        Expression<Func<TicketIndex, bool>> condition,
        Expression<Func<TicketIndex, DateTime>> order,
        CancellationToken cancellationToken)
    {
        var predicate = TicketScopeFilter.ToPredicate(scope);
        var facts = new List<TicketFacts>();

        await foreach (var item in ReportIndexPages.ReadAsync<ContentItem, TicketIndex>(_session, rows => rows.Where(predicate).Where(condition), order, null, cancellationToken))
        {
            if (item.TryGet<TicketPart>(out var part) && string.IsNullOrEmpty(part.MergedIntoId))
            {
                facts.Add(new TicketFacts(
                    item.ContentItemId,
                    item.ContentType,
                    part.DeskId,
                    part.TeamId,
                    part.AssigneeId,
                    part.PriorityId,
                    part.StatusCategory,
                    part.Channel,
                    item.CreatedUtc ?? DateTime.MinValue,
                    part.FirstResponseUtc,
                    part.SolvedUtc,
                    part.NextDueUtc,
                    part.ReplyCount,
                    part.ReopenCount,
                    part.EscalationLevel));
            }
        }

        return facts;
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
