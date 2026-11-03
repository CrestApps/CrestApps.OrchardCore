using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Calendar;

/// <summary>
/// Places activities on their owner's calendar as an event, and keeps the event at the activity's scheduled time.
/// </summary>
public sealed class ActivityCalendarService
{
    /// <summary>
    /// The source of the calendar events this feature makes, and the actor of the changes it makes to them.
    /// </summary>
    public const string Source = "OmnichannelActivity";

    /// <summary>
    /// How long the event of an activity lasts when it is placed.
    /// </summary>
    public const int DurationMinutes = 15;

    private static readonly CalendarChangeOptions _syncOptions = new()
    {
        Actor = Source,
        SkipConflictCheck = true,
        SendInvitations = false,
    };

    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityCalendarService"/> class.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="eventManager">The calendar event manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivityCalendarService(
        ICalendarService calendarService,
        ICalendarEventManager eventManager,
        IContentManager contentManager,
        IStringLocalizer<ActivityCalendarService> stringLocalizer)
    {
        _calendarService = calendarService;
        _eventManager = eventManager;
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Places an activity on a user's personal calendar at its scheduled time, and links the two. The caller saves the
    /// activity.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="userId">The user whose calendar it goes on.</param>
    /// <param name="userName">The user's name, for a personal calendar made for them.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The event, or <see langword="null"/> when it couldn't be placed.</returns>
    public async Task<CalendarEvent> PlaceAsync(OmnichannelActivity activity, string userId, string userName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (string.IsNullOrEmpty(userId) || activity.ScheduledUtc == default)
        {
            return null;
        }

        if (await FindEventAsync(activity, cancellationToken) is { } existing)
        {
            return existing;
        }

        var calendar = await _calendarService.GetOrCreatePersonalCalendarAsync(userId, userName, cancellationToken);

        if (calendar is null)
        {
            return null;
        }

        var calendarEvent = await _eventManager.NewAsync(calendar, startUtc: activity.ScheduledUtc, cancellationToken: cancellationToken);

        calendarEvent.Title = await GetTitleAsync(activity);
        calendarEvent.AllDay = false;
        calendarEvent.Start = CalendarTime.ToLocal(activity.ScheduledUtc, calendarEvent.TimeZoneId);
        calendarEvent.End = calendarEvent.Start.AddMinutes(DurationMinutes);
        calendarEvent.Location = activity.PreferredDestination;
        calendarEvent.Description = activity.Instructions;
        calendarEvent.Source = Source;
        calendarEvent.SourceId = activity.ItemId;

        var result = await _eventManager.CreateAsync(calendarEvent, new CalendarChangeOptions { Actor = userId, SkipConflictCheck = true }, cancellationToken);

        if (!result.Succeeded)
        {
            return null;
        }

        activity.Put(new ActivityCalendarLink { EventId = result.Value.ItemId, CalendarId = calendar.ItemId });

        return result.Value;
    }

    /// <summary>
    /// Brings an activity's event in step with it: moved to its scheduled time, or cancelled with it. A finished activity leaves
    /// its event as a record of what was planned.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task SyncEventAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var calendarEvent = await FindEventAsync(activity, cancellationToken);

        if (calendarEvent is null || calendarEvent.Status == CalendarEventStatus.Cancelled)
        {
            return;
        }

        if (activity.Status is ActivityStatus.Cancelled or ActivityStatus.Purged)
        {
            await _eventManager.CancelAsync(calendarEvent, _syncOptions, cancellationToken);

            return;
        }

        if (activity.Status.IsTerminal() || activity.ScheduledUtc == default || calendarEvent.StartUtc == activity.ScheduledUtc)
        {
            return;
        }

        await _eventManager.UpdateAsync(calendarEvent, target =>
        {
            var length = target.End - target.Start;

            target.Start = CalendarTime.ToLocal(activity.ScheduledUtc, target.TimeZoneId);
            target.End = target.Start + (length > TimeSpan.Zero ? length : TimeSpan.FromMinutes(DurationMinutes));
        }, _syncOptions, cancellationToken);
    }

    /// <summary>
    /// Deletes a deleted activity's event.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task DeleteEventAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (await FindEventAsync(activity, cancellationToken) is { } calendarEvent)
        {
            await _eventManager.DeleteAsync(calendarEvent, _syncOptions, cancellationToken);
        }
    }

    private async Task<CalendarEvent> FindEventAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        return activity.TryGet<ActivityCalendarLink>(out var link) && !string.IsNullOrEmpty(link.EventId)
            ? await _eventManager.FindByIdAsync(link.EventId, cancellationToken)
            : null;
    }

    private async Task<string> GetTitleAsync(OmnichannelActivity activity)
    {
        var contact = string.IsNullOrEmpty(activity.ContactContentItemId)
            ? null
            : await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);
        var name = !string.IsNullOrWhiteSpace(contact?.DisplayText)
            ? contact.DisplayText
            : activity.PreferredDestination;

        if (string.IsNullOrWhiteSpace(name))
        {
            return !string.IsNullOrWhiteSpace(activity.Subject?.DisplayText) ? activity.Subject.DisplayText : S["Follow-up activity"];
        }

        return string.Equals(activity.Channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase)
            ? S["Call {0}", name]
            : S["Follow up with {0}", name];
    }
}
