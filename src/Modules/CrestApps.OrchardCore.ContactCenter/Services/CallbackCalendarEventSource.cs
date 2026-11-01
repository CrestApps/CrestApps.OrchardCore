using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Shows the open callbacks reserved for an agent on the calendar, as read-only occurrences that are never stored as
/// calendar events. A callback is shown for the agents a calendar query asks for: the users whose invitations it reads,
/// and the owners of the personal calendars it reads, on that calendar. With a viewer, only the viewer's own callbacks
/// are shown, unless the viewer may monitor the Contact Center, and only the agent a callback is for and those who may
/// monitor the Contact Center see the number to call back.
/// </summary>
internal sealed class CallbackCalendarEventSource : ICalendarEventSource
{
    /// <summary>
    /// The name of the source, used as the source of the occurrences it returns.
    /// </summary>
    public const string SourceName = "ContactCenter.Callbacks";

    /// <summary>
    /// The event type of the occurrences the source returns.
    /// </summary>
    public const string EventType = "Callback";

    /// <summary>
    /// The color callbacks are drawn in.
    /// </summary>
    public const string Color = "#f08c00";

    /// <summary>
    /// How long a callback is shown to last, since it is a moment rather than a length of time.
    /// </summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);

    private readonly ICallbackRequestStore _callbackStore;
    private readonly IAgentProfileStore _agentProfileStore;
    private readonly ICalendarService _calendarService;
    private readonly IAuthorizationService _authorizationService;
    private readonly CalendarOptions _options;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackCalendarEventSource"/> class.
    /// </summary>
    /// <param name="callbackStore">The callback store.</param>
    /// <param name="agentProfileStore">The agent profile store.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="options">The calendar options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallbackCalendarEventSource(
        ICallbackRequestStore callbackStore,
        IAgentProfileStore agentProfileStore,
        ICalendarService calendarService,
        IAuthorizationService authorizationService,
        IOptions<CalendarOptions> options,
        IStringLocalizer<CallbackCalendarEventSource> stringLocalizer)
    {
        _callbackStore = callbackStore;
        _agentProfileStore = agentProfileStore;
        _calendarService = calendarService;
        _authorizationService = authorizationService;
        _options = options.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => SourceName;

    /// <inheritdoc/>
    public async Task<IEnumerable<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Matches(query))
        {
            return [];
        }

        var fromUtc = DateTime.SpecifyKind(query.FromUtc, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(query.ToUtc, DateTimeKind.Utc);

        if (toUtc <= fromUtc)
        {
            return [];
        }

        // Without a viewer the caller is trusted, such as background work; with one, only a supervisor sees everyone's.
        var seesEveryone = query.Viewer is null ||
            await _authorizationService.AuthorizeAsync(query.Viewer, ContactCenterPermissions.MonitorContactCenter);
        var viewerId = query.Viewer?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!seesEveryone && string.IsNullOrEmpty(viewerId))
        {
            return [];
        }

        var calendarByUser = await GetPersonalCalendarsAsync(query, cancellationToken);
        var userIds = new HashSet<string>(query.AttendeeUserIds.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);

        userIds.UnionWith(calendarByUser.Keys);

        if (!seesEveryone)
        {
            // Someone who can't see everyone's callbacks sees their own, and only when the query is about them.
            if (query.AllCalendars)
            {
                userIds.Add(viewerId);
            }

            userIds.IntersectWith([viewerId]);
        }

        var agents = new Dictionary<string, AgentProfile>(StringComparer.Ordinal);
        IReadOnlyCollection<CallbackRequest> callbacks;

        // A callback is shown for a while after its time, so those due a moment before the range overlap it too.
        var startsAfterUtc = fromUtc - Duration;

        if (query.AllCalendars && seesEveryone)
        {
            callbacks = await _callbackStore.ListOpenForAgentsAsync(null, startsAfterUtc, toUtc, _options.MaxEventsPerQuery, cancellationToken);

            var agentIds = callbacks.Select(callback => callback.AgentId).Distinct(StringComparer.Ordinal).ToArray();

            if (agentIds.Length > 0)
            {
                foreach (var agent in await _agentProfileStore.GetAsync(agentIds, cancellationToken))
                {
                    agents[agent.ItemId] = agent;
                }
            }
        }
        else
        {
            foreach (var userId in userIds)
            {
                var agent = await _agentProfileStore.FindByUserIdAsync(userId, cancellationToken);

                if (agent is not null)
                {
                    agents[agent.ItemId] = agent;
                }
            }

            if (agents.Count == 0)
            {
                return [];
            }

            callbacks = await _callbackStore.ListOpenForAgentsAsync(agents.Keys, startsAfterUtc, toUtc, _options.MaxEventsPerQuery, cancellationToken);
        }

        var occurrences = new List<CalendarOccurrence>(callbacks.Count);

        foreach (var callback in callbacks)
        {
            agents.TryGetValue(callback.AgentId ?? string.Empty, out var agent);

            // Only the agent the callback is for, and those who see everyone's, see who is to be called back.
            var showsDestination = query.Viewer is not null &&
                (seesEveryone || (agent is not null && string.Equals(agent.UserId, viewerId, StringComparison.Ordinal)));
            var calendarId = agent?.UserId is { Length: > 0 } userId
                ? calendarByUser.GetValueOrDefault(userId)
                : null;

            occurrences.Add(ToOccurrence(callback, calendarId, showsDestination));
        }

        return occurrences;
    }

    private static bool Matches(CalendarOccurrenceQuery query)
    {
        if (query.Sources.Count > 0 && !query.Sources.Contains(SourceName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.EventTypes.Count > 0 && !query.EventTypes.Contains(EventType, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    // The owners of the personal calendars the query reads, with the calendar each one's callbacks go on.
    private async Task<Dictionary<string, string>> GetPersonalCalendarsAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken)
    {
        var calendarByUser = new Dictionary<string, string>(StringComparer.Ordinal);

        if (query.CalendarIds.Count == 0)
        {
            return calendarByUser;
        }

        foreach (var calendar in await _calendarService.GetAsync(query.CalendarIds, cancellationToken))
        {
            if (calendar.OwnerKind == CalendarOwnerKind.User && !string.IsNullOrEmpty(calendar.OwnerId))
            {
                calendarByUser.TryAdd(calendar.OwnerId, calendar.ItemId);
            }
        }

        return calendarByUser;
    }

    private CalendarOccurrence ToOccurrence(CallbackRequest callback, string calendarId, bool showsDestination)
    {
        var startUtc = DateTime.SpecifyKind(callback.ScheduledUtc, DateTimeKind.Utc);
        var endUtc = startUtc + Duration;
        var title = showsDestination && !string.IsNullOrWhiteSpace(callback.Destination)
            ? S["Call back {0}", callback.Destination.Trim()].Value
            : S["Callback"].Value;

        var calendarEvent = new CalendarEvent
        {
            ItemId = callback.ItemId,
            CalendarId = calendarId,
            EventType = EventType,
            Title = title,
            Start = DateTime.SpecifyKind(startUtc, DateTimeKind.Unspecified),
            End = DateTime.SpecifyKind(endUtc, DateTimeKind.Unspecified),
            TimeZoneId = "UTC",
            StartUtc = startUtc,
            EndUtc = endUtc,
            Status = CalendarEventStatus.Confirmed,
            Transparency = CalendarTransparency.Busy,
            Color = Color,
            Source = SourceName,
            SourceId = callback.ItemId,
            Editable = false,
            CreatedUtc = callback.CreatedUtc,
            ModifiedUtc = callback.ModifiedUtc ?? callback.CreatedUtc,
        };

        return new CalendarOccurrence
        {
            Event = calendarEvent,
            SeriesId = callback.ItemId,
            OriginalStartUtc = startUtc,
            Start = calendarEvent.Start,
            End = calendarEvent.End,
            StartUtc = startUtc,
            EndUtc = endUtc,
        };
    }
}
