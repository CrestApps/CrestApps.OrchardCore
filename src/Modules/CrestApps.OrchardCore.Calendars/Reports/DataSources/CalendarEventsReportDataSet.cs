using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Calendars.Reports.DataSources;

/// <summary>
/// One row per stored calendar event on a calendar the principal may read: a one-time event, a repeating event (once,
/// not once per occurrence), or a changed occurrence of one. Events on calendars the principal can only see as busy
/// time are left out. A private event on a calendar the principal can read but not change keeps its time and loses its
/// title and attendee count, as it shows as busy time on the calendar. Descriptions, locations, attendees' names and
/// addresses, and links to external calendars are never exposed.
/// </summary>
internal sealed class CalendarEventsReportDataSet : IndexedReportDataSet<CalendarEvent, CalendarEventIndex, CalendarEventReportRow>
{
    /// <summary>
    /// The name of the field that holds when the event starts, the data set's main date.
    /// </summary>
    public const string StartUtcField = "StartUtc";

    private readonly CalendarReportAccess _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session events are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Works out which calendars the principal may read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarEventsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        CalendarReportAccess access,
        IStringLocalizer<CalendarEventsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CalendarReportDataSets.Events,
                stringLocalizer["Calendar events"],
                stringLocalizer["One row per event on a calendar you can read: when it is, how long, its type, status and source. A repeating event is one row."]),
            session,
            authorizationService,
            CalendarsConstants.CollectionName,
            CalendarPermissions.AccessCalendars)
    {
        _access = access;

        var S = stringLocalizer;
        var calendarEvent = S["Event"].Value;
        var calendar = S["Calendar"].Value;
        var dates = S["Dates"].Value;

        AddField(CalendarReportDataSets.ItemIdField, S["Event ID"], DataFieldType.Text, row => row.Event.ItemId, calendarEvent).Identifier();
        AddField(nameof(CalendarEvent.EventType), S["Event type"], DataFieldType.Text, row => row.Event.EventType, calendarEvent).Description = S["The kind of event, such as Event, Meeting, Shift or Appointment."].Value;
        AddField(nameof(CalendarEvent.Title), S["Title"], DataFieldType.Text, row => row.DetailsHidden ? null : row.Event.Title, calendarEvent).Description = S["Empty for a private event on a calendar you can read but not change."].Value;
        AddField(nameof(CalendarEvent.Status), S["Status"], DataFieldType.Text, row => row.Event.Status, calendarEvent).Description = S["Confirmed, Tentative or Cancelled."].Value;
        AddField(nameof(CalendarEvent.Visibility), S["Visibility"], DataFieldType.Text, row => row.Event.Visibility, calendarEvent).Description = S["Default, Public or Private."].Value;
        AddField(nameof(CalendarEventReportRow.DetailsHidden), S["Details hidden"], DataFieldType.Boolean, row => row.DetailsHidden, calendarEvent).Description = S["Whether the event's title and attendees are hidden from you because it is private."].Value;
        AddField(nameof(CalendarEvent.Transparency), S["Busy or free"], DataFieldType.Text, row => row.Event.Transparency, calendarEvent).Description = S["Busy, or Free when the event leaves the time free."].Value;
        AddField("AttendeeCount", S["Attendees"], DataFieldType.Integer, row => row.DetailsHidden ? null : (int?)(row.Event.Attendees?.Count ?? 0), calendarEvent).Description = S["How many people, rooms and equipment are invited; empty when the details are hidden."].Value;
        AddField("IsRecurring", S["Repeats"], DataFieldType.Boolean, row => !string.IsNullOrWhiteSpace(row.Event.Recurrence?.Rule), calendarEvent);
        AddField(nameof(CalendarEvent.SeriesId), S["Series ID"], DataFieldType.Text, row => row.Event.SeriesId, calendarEvent)
            .Identifier(new DataFieldReference(CalendarReportDataSets.Source, CalendarReportDataSets.Events, CalendarReportDataSets.ItemIdField))
            .Description = S["The repeating event a changed occurrence belongs to; empty for other events."].Value;
        AddField(nameof(CalendarEvent.Source), S["Source"], DataFieldType.Text, row => row.Event.Source, calendarEvent).Description = S["The module that owns the event, such as Scheduling; empty for events made on the calendar."].Value;
        AddField(nameof(CalendarEvent.SourceId), S["Source ID"], DataFieldType.Text, row => row.Event.SourceId, calendarEvent).Identifier();

        AddField(CalendarReportDataSets.CalendarIdField, S["Calendar ID"], DataFieldType.Text, row => row.Event.CalendarId, calendar)
            .Identifier(new DataFieldReference(CalendarReportDataSets.Source, CalendarReportDataSets.Calendars, CalendarReportDataSets.ItemIdField));
        AddField(nameof(CalendarEventReportRow.CalendarType), S["Calendar type"], DataFieldType.Text, row => row.CalendarType, calendar);

        AddField(StartUtcField, S["Starts"], DataFieldType.DateTime, row => row.Event.StartUtc, dates).Description = S["For a repeating event, when its first occurrence starts."].Value;
        AddField(nameof(CalendarEvent.EndUtc), S["Ends"], DataFieldType.DateTime, row => row.Event.EndUtc, dates);
        AddField(nameof(CalendarEvent.AllDay), S["All day"], DataFieldType.Boolean, row => row.Event.AllDay, dates);
        AddField("DurationMinutes", S["Duration (minutes)"], DataFieldType.Integer, row => GetDurationMinutes(row.Event), dates);
        AddField(nameof(CalendarEvent.TimeZoneId), S["Time zone"], DataFieldType.Text, row => row.Event.TimeZoneId, dates);
        AddField(nameof(CalendarEvent.CreatedUtc), S["Created"], DataFieldType.DateTime, row => row.Event.CreatedUtc, dates);
        AddField(nameof(CalendarEvent.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, row => row.Event.ModifiedUtc, dates);
        AddField(nameof(CalendarEvent.CreatedBy), S["Created by"], DataFieldType.Text, row => row.Event.CreatedBy, dates).Identifier(ReportDataReferences.User());

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CalendarEventIndex, DateTime>> Column)? DateColumn
        => (StartUtcField, index => index.StartUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CalendarEventIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CalendarEventIndex, string>>>(StringComparer.Ordinal)
        {
            [CalendarReportDataSets.ItemIdField] = index => index.ItemId,
            [CalendarReportDataSets.CalendarIdField] = index => index.CalendarId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CalendarEventIndex> columns)
    {
        columns
            .Enum(nameof(CalendarEvent.Status), index => index.Status)
            .Enum(nameof(CalendarEvent.Transparency), index => index.Transparency);
    }

    /// <inheritdoc/>
    protected override IEnumerable<CalendarEventReportRow> GetRows(CalendarEvent document)
    {
        yield return new CalendarEventReportRow(document);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<CalendarEventReportRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var user = query.Context?.User;
        var scope = await _access.GetScopeAsync(user, cancellationToken);

        if (scope is null)
        {
            yield break;
        }

        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            var access = await _access.FindAsync(user, scope, row.Event.CalendarId, cancellationToken);

            // An event whose calendar is gone, or that the principal can't read, is never returned.
            if (access is null || access.Level < CalendarAccessLevel.Read)
            {
                continue;
            }

            row.CalendarType = access.Calendar.Type;
            row.DetailsHidden = CalendarReportScope.HidesDetails(row.Event, access.Level);

            yield return row;
        }
    }

    /// <inheritdoc/>
    protected override async Task<Func<IQueryIndex<CalendarEventIndex>, IQueryIndex<CalendarEventIndex>>> RestrictAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetScopeAsync(context?.User, cancellationToken);

        if (scope is null)
        {
            return null;
        }

        if (scope.EveryCalendar)
        {
            return rows => rows;
        }

        var ids = scope.CalendarIds.ToArray();

        return rows => rows.Where(index => index.CalendarId.IsIn(ids));
    }

    private static long GetDurationMinutes(CalendarEvent calendarEvent)
    {
        return calendarEvent.EndUtc > calendarEvent.StartUtc
            ? (long)Math.Round((calendarEvent.EndUtc - calendarEvent.StartUtc).TotalMinutes, MidpointRounding.AwayFromZero)
            : 0;
    }
}
