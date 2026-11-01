using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IWorkerFeedService"/>. The feed's secret is kept in the worker's portal settings and found
/// by its hash.
/// </summary>
internal sealed class DefaultWorkerFeedService : IWorkerFeedService
{
    private readonly ISession _session;
    private readonly WorkerStore _workerStore;
    private readonly TeamStore _teamStore;
    private readonly WorkerShiftReader _shiftReader;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultWorkerFeedService(ISession session, WorkerStore workerStore, TeamStore teamStore, WorkerShiftReader shiftReader, IClock clock, IStringLocalizer<DefaultWorkerFeedService> stringLocalizer)
    {
        _session = session;
        _workerStore = workerStore;
        _teamStore = teamStore;
        _shiftReader = shiftReader;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<string> GetTokenAsync(WorkerProfile worker, bool renew, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var settings = worker.TryGet<WorkerPortalSettings>(out var stored) ? stored : new WorkerPortalSettings();

        if (!renew && !string.IsNullOrEmpty(settings.FeedToken))
        {
            return settings.FeedToken;
        }

        settings.FeedToken = PortalSecrets.NewToken();
        worker.Put(settings);
        await _workerStore.UpdateAsync(worker, cancellationToken);

        return settings.FeedToken;
    }

    public async Task<string> WriteAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
        {
            return null;
        }

        var hash = PortalSecrets.Hash(token);
        var worker = await _session.Query<WorkerProfile, WorkerFeedIndex>(index => index.TokenHash == hash, collection: WorkforceConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        // The hash finds the worker; the secret itself must still match, so an old index row can't open the feed.
        if (worker is null || !worker.TryGet<WorkerPortalSettings>(out var settings) || !string.Equals(settings.FeedToken, token, StringComparison.Ordinal))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var fromUtc = now.AddDays(-28);
        var toUtc = now.AddDays(84);
        var teams = (await _teamStore.ListAsync(cancellationToken)).ToDictionary(team => team.ItemId, team => team.Name, StringComparer.Ordinal);
        var events = new List<CalendarEvent>();

        foreach (var shift in await _shiftReader.ListAsync(worker, fromUtc, toUtc, cancellationToken))
        {
            var teamName = ShiftEvents.TryRead(shift, out var data) && teams.TryGetValue(data.TeamId ?? string.Empty, out var name) ? name : null;

            events.Add(Copy(shift, teamName is null ? S["Shift"] : S["Shift: {0}", teamName]));
        }

        foreach (var timeOff in await _shiftReader.ListTimeOffAsync(worker, fromUtc, toUtc, cancellationToken))
        {
            events.Add(Copy(timeOff, S["Time off: {0}", (timeOff.Title ?? string.Empty).Split(':').Last().Trim()]));
        }

        return ICalendarWriter.Write(S["{0}'s work", worker.DisplayName], events, now);
    }

    // A copy, so the feed's titles never touch the stored events.
    private static CalendarEvent Copy(CalendarEvent source, string title)
    {
        return new CalendarEvent
        {
            ItemId = source.ItemId,
            Uid = source.Uid,
            CalendarId = source.CalendarId,
            EventType = source.EventType,
            Title = title,
            AllDay = source.AllDay,
            Start = source.Start,
            End = source.End,
            TimeZoneId = source.TimeZoneId,
            StartUtc = source.StartUtc,
            EndUtc = source.EndUtc,
            Status = source.Status,
            Transparency = source.Transparency,
            Sequence = source.Sequence,
            CreatedUtc = source.CreatedUtc,
            ModifiedUtc = source.ModifiedUtc,
        };
    }
}
