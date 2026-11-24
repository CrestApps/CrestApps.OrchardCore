using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Builds the results of the calendar tools from an explicit list of fields. A calendar's feed token, its subscription's
/// address, sync token and pending deletions, its other properties and an attendee's answer token are never returned.
/// </summary>
internal static class CalendarToolResults
{
    /// <summary>
    /// Describes a calendar. Its shares, feed address and subscription are included only for someone who manages it.
    /// </summary>
    public static async Task<object> DescribeCalendarAsync(IServiceProvider services, Calendar calendar, CalendarAccessLevel level)
    {
        var typeManager = services.GetRequiredService<ICalendarTypeManager>();
        var localizerFactory = services.GetService<IStringLocalizerFactory>();
        var type = typeManager.FindType(calendar.Type);
        var manages = level >= CalendarAccessLevel.Manage;

        return new
        {
            id = calendar.ItemId,
            name = calendar.Name,
            description = calendar.Description,
            type = calendar.Type,
            typeName = Localize(localizerFactory, type?.DisplayName) ?? calendar.Type,
            color = string.IsNullOrEmpty(calendar.Color) ? type?.DefaultColor : calendar.Color,
            timeZone = calendar.TimeZoneId,
            ownerKind = calendar.OwnerKind.ToString(),
            ownerId = calendar.OwnerId,
            visibility = calendar.Visibility.ToString(),
            enabled = calendar.Enabled,
            limitOverlaps = calendar.ConflictPolicy == CalendarConflictPolicy.LimitConcurrent,
            capacity = Math.Max(1, calendar.Capacity),
            managedBy = calendar.Source,
            access = level.ToString(),
            canAddEvents = calendar.Enabled && level >= CalendarAccessLevel.Write,
            details = DescribeDetails(calendar),
            eventTypes = (type?.EventTypes ?? []).Select(eventType => DescribeEventType(localizerFactory, eventType)),
            exportPage = level >= CalendarAccessLevel.Read ? CalendarToolSupport.GetAdminPath(services, "CalendarFeeds", "Export", new { id = calendar.ItemId }) : null,
            shares = manages ? await DescribeSharesAsync(services, calendar) : null,
            hasFeedAddress = manages ? !string.IsNullOrEmpty(calendar.FeedToken) : (bool?)null,
            subscription = manages ? await DescribeSubscriptionAsync(services, calendar) : null,
        };
    }

    /// <summary>
    /// Describes an event in full, with its attendees and whether the caller can change it.
    /// </summary>
    public static async Task<object> DescribeEventAsync(IServiceProvider services, ClaimsPrincipal user, CalendarEvent calendarEvent, Calendar calendar)
    {
        var editor = services.GetRequiredService<CalendarEventEditor>();
        var level = await services.GetRequiredService<ICalendarAccessService>().GetAccessLevelAsync(user, calendar);
        var userId = CalendarUser.GetId(user);
        var mine = string.IsNullOrEmpty(userId) ? null : calendarEvent.Attendees.FirstOrDefault(attendee => attendee.UserId == userId);
        var category = string.IsNullOrEmpty(calendarEvent.CategoryId)
            ? null
            : await services.GetRequiredService<ICalendarCategoryService>().FindByIdAsync(calendarEvent.CategoryId);
        var attendees = new List<object>();

        foreach (var attendee in calendarEvent.Attendees)
        {
            attendees.Add(new
            {
                key = attendee.GetKey(),
                userId = attendee.UserId,
                name = attendee.Name,
                email = attendee.Email,
                resourceCalendarId = attendee.ResourceCalendarId,
                role = attendee.Role.ToString(),
                response = attendee.Response.ToString(),
                respondedAt = await CalendarToolSupport.ToLocalAsync(services, attendee.RespondedUtc, calendarEvent.TimeZoneId ?? calendar.TimeZoneId),
            });
        }

        return new
        {
            id = calendarEvent.ItemId,
            calendarId = calendar.ItemId,
            calendar = calendar.Name,
            eventType = calendarEvent.EventType,
            title = calendarEvent.Title,
            description = calendarEvent.Description,
            location = calendarEvent.Location,
            allDay = calendarEvent.AllDay,
            start = CalendarToolSupport.FormatWallClock(calendarEvent.Start, calendarEvent.AllDay),
            end = CalendarToolSupport.FormatWallClock(calendarEvent.AllDay ? calendarEvent.End.AddDays(-1) : calendarEvent.End, calendarEvent.AllDay),
            timeZone = calendarEvent.TimeZoneId,
            recurrence = calendarEvent.Recurrence is null ? null : new
            {
                rule = calendarEvent.Recurrence.Rule,
                exceptionDates = calendarEvent.Recurrence.ExceptionDates.Select(date => CalendarToolSupport.FormatWallClock(date, calendarEvent.AllDay)),
            },
            seriesId = calendarEvent.SeriesId,
            originalStart = calendarEvent.OriginalStartUtc is { } original
                ? CalendarToolSupport.FormatWallClock(CalendarTime.ToLocal(original, calendarEvent.TimeZoneId), calendarEvent.AllDay)
                : null,
            status = calendarEvent.Status.ToString(),
            transparency = calendarEvent.Transparency.ToString(),
            visibility = calendarEvent.Visibility.ToString(),
            color = calendarEvent.Color,
            category = category is null ? null : new { id = category.ItemId, name = category.Name, color = category.Color },
            reminderMinutes = calendarEvent.ReminderMinutes,
            attendees,
            myResponse = mine?.Response.ToString(),
            managedBy = calendarEvent.Source,
            canChange = await editor.CanChangeAsync(user, calendarEvent, calendar),
            canConfirm = calendarEvent.Status == CalendarEventStatus.Tentative && level >= CalendarAccessLevel.Manage,
        };
    }

    /// <summary>
    /// Gets the scheme and host of a subscription address, which is all a tool shows of it: many private calendar addresses
    /// carry a secret in their path or query.
    /// </summary>
    public static string GetAddressHost(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var value = url.Trim();

        if (value.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
        {
            value = string.Concat("https://", value.AsSpan(9));
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority).Replace(uri.UserInfo + "@", string.Empty, StringComparison.Ordinal) : null;
    }

    /// <summary>
    /// Removes a subscription address from a text, such as the last sync error.
    /// </summary>
    public static string ScrubAddress(string text, string url)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(url))
        {
            return text;
        }

        var value = url.Trim();
        var result = text.Replace(value, "[the subscription address]", StringComparison.OrdinalIgnoreCase);

        if (value.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace(string.Concat("https://", value.AsSpan(9)), "[the subscription address]", StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Describes a calendar's subscription to an external calendar, or <see langword="null"/> when the calendar cannot have one.
    /// </summary>
    public static async Task<object> DescribeSubscriptionAsync(IServiceProvider services, Calendar calendar)
    {
        var syncService = services.GetService<CalendarSyncService>();

        if (syncService is null || !CalendarSubscriptionRules.Supports(calendar, services.GetRequiredService<ICalendarTypeManager>()))
        {
            return null;
        }

        var localizerFactory = services.GetService<IStringLocalizerFactory>();
        var subscription = calendar.TryGet<CalendarSubscription>(out var stored) ? stored : null;

        return new
        {
            configured = subscription is not null && (!string.IsNullOrEmpty(subscription.Url) || !string.IsNullOrEmpty(subscription.Provider)),
            provider = subscription?.Provider ?? CalendarSyncService.DefaultProvider,
            hasAddress = !string.IsNullOrEmpty(subscription?.Url),
            addressHost = GetAddressHost(subscription?.Url),
            enabled = subscription?.Enabled == true && !string.IsNullOrEmpty(subscription.Url),
            intervalMinutes = Math.Max(CalendarSubscriptionRules.MinIntervalMinutes, subscription?.IntervalMinutes ?? 60),
            twoWay = subscription?.TwoWay ?? false,
            conflictPolicy = (subscription?.ConflictPolicy ?? CalendarSyncConflictPolicy.ExternalWins).ToString(),
            lastSynced = await CalendarToolSupport.ToLocalAsync(services, subscription?.LastSyncedUtc, calendar.TimeZoneId),
            lastAttempt = await CalendarToolSupport.ToLocalAsync(services, subscription?.LastAttemptUtc, calendar.TimeZoneId),
            lastResult = subscription?.LastResult,
            lastError = ScrubAddress(subscription?.LastError, subscription?.Url),
            failuresInARow = subscription?.Failures ?? 0,
            needsReconnect = subscription?.NeedsReconnect ?? false,
            availableProviders = syncService.ListProviders().Select(provider => new
            {
                name = provider.Name,
                displayName = Localize(localizerFactory, provider.DisplayName) ?? provider.Name,
                usesAddress = provider.UsesAddress,
                canWriteBack = provider is IWritableCalendarProvider,
            }),
        };
    }

    /// <summary>
    /// Describes an event type of a calendar type.
    /// </summary>
    public static object DescribeEventType(IStringLocalizerFactory localizerFactory, CalendarEventTypeDescriptor eventType)
        => new
        {
            name = eventType.Name,
            displayName = Localize(localizerFactory, eventType.DisplayName) ?? eventType.Name,
            color = eventType.Color,
            busy = eventType.DefaultTransparency == CalendarTransparency.Busy,
            allDayByDefault = eventType.AllDayByDefault,
            defaultDurationMinutes = eventType.DefaultDurationMinutes,
            defaultReminderMinutes = eventType.DefaultReminderMinutes,
            requiresApproval = eventType.RequiresApproval,
            requiresAttendee = eventType.RequiresAttendee,
            countsTowardCapacity = eventType.CountsTowardCapacity,
        };

    /// <summary>
    /// Localizes a display name, or returns <see langword="null"/> when there is none.
    /// </summary>
    public static string Localize(IStringLocalizerFactory localizerFactory, LocalizationSource source)
    {
        if (source is null)
        {
            return null;
        }

        return localizerFactory is null ? source.Value : localizerFactory.Localize(source).Value;
    }

    private static object DescribeDetails(Calendar calendar)
    {
        if (string.Equals(calendar.Type, CalendarsConstants.Types.Room, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(calendar.Type, CalendarsConstants.Types.Equipment, StringComparison.OrdinalIgnoreCase))
        {
            var settings = calendar.TryGet<ResourceCalendarSettings>(out var resource) ? resource : new ResourceCalendarSettings();

            return new { location = settings.Location, seats = settings.Seats, amenities = settings.Amenities };
        }

        if (string.Equals(calendar.Type, CalendarsConstants.Types.Office, StringComparison.OrdinalIgnoreCase))
        {
            var settings = calendar.TryGet<OfficeCalendarSettings>(out var office) ? office : new OfficeCalendarSettings();

            return new { address = settings.Address, contact = settings.Contact };
        }

        return null;
    }

    private static async Task<object> DescribeSharesAsync(IServiceProvider services, Calendar calendar)
    {
        var userIds = calendar.Shares.Where(share => share.PrincipalKind == CalendarPrincipalKind.User).Select(share => share.PrincipalId).ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var userLookup = userIds.Count == 0 ? null : services.GetService<CalendarUserLookup>();

        if (userLookup is not null)
        {
            foreach (var item in await userLookup.GetAsync(userIds))
            {
                names[item.Value] = item.Text;
            }
        }

        return calendar.Shares.Select(share => new
        {
            kind = share.PrincipalKind.ToString(),
            id = share.PrincipalId,
            name = share.PrincipalKind == CalendarPrincipalKind.Role ? share.PrincipalId : names.GetValueOrDefault(share.PrincipalId) ?? share.DisplayName,
            access = share.Access.ToString(),
        }).ToList();
    }
}
