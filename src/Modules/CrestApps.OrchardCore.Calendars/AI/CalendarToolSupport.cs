using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Helpers the calendar administration, feed and subscription tools share: reading arguments, paging, writing results,
/// showing times and linking to the calendar pages.
/// </summary>
internal static class CalendarToolSupport
{
    /// <summary>
    /// The page size a list returns when none is asked for.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The largest page size a list returns.
    /// </summary>
    public const int MaxPageSize = 50;

    /// <summary>
    /// The answer when a calendar does not exist or the caller cannot see it. The two are not told apart.
    /// </summary>
    public const string CalendarNotFound = "There is no calendar with that ID that the user can see. Use listCalendars to find it.";

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    public static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    /// <summary>
    /// Gets whether an argument was sent with a value.
    /// </summary>
    public static bool Has(AIFunctionArguments arguments, string name)
        => arguments is not null && arguments.TryGetValue(name, out var value) && value is not null && value is not JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    /// <summary>
    /// Reads a text argument, keeping an empty text as empty (so a field can be cleared).
    /// </summary>
    /// <returns>The trimmed text, or <see langword="null"/> when it was not sent.</returns>
    public static string ReadRaw(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var value = arguments[name];
        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return text?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <returns>The value, or <see langword="null"/> when it was not sent or is not true or false.</returns>
    public static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(ReadRaw(arguments, name), out var value) ? value : null;

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <returns>The value, or <see langword="null"/> when it was not sent or is not a whole number.</returns>
    public static int? ReadInt(AIFunctionArguments arguments, string name)
    {
        var text = ReadRaw(arguments, name);

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue
            ? (int)real
            : null;
    }

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    /// <summary>
    /// Reads an argument that is an array of objects.
    /// </summary>
    /// <returns>The objects, or <see langword="null"/> when the argument was not sent or is not an array of objects.</returns>
    public static IReadOnlyList<JsonElement> ReadObjects(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var element = arguments[name] is JsonElement json ? json : JsonSerializer.SerializeToElement(arguments[name], _jsonOptions);

        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = element.EnumerateArray().ToList();

        return items.All(item => item.ValueKind == JsonValueKind.Object) ? items : null;
    }

    /// <summary>
    /// Reads a text property of an object argument.
    /// </summary>
    public static string GetText(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString().Trim()
            : null;

    /// <summary>
    /// Shows a stored UTC time in the user's or the site's time zone, or in the given zone when the local clock is not available.
    /// </summary>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    public static async Task<string> ToLocalAsync(IServiceProvider services, DateTime? utc, string fallbackTimeZoneId)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var value = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        var localClock = services?.GetService<ILocalClock>();

        if (localClock is not null)
        {
            var local = await localClock.ConvertToLocalAsync(value);

            return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        }

        var wallClock = CalendarTime.ToLocal(value, CalendarTime.NormalizeZoneId(fallbackTimeZoneId) ?? "UTC");

        return new DateTimeOffset(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), wallClock - value).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Shows an event's wall-clock time in its own time zone, as the other calendar tools do: a date for an all-day event.
    /// </summary>
    public static string FormatWallClock(DateTime value, bool allDay)
        => value.ToString(allDay ? "yyyy-MM-dd" : "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the path of a calendar admin page.
    /// </summary>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    public static string GetAdminPath(IServiceProvider services, string controller, string action, object values = null)
    {
        var httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services?.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values)
        {
            ["area"] = CalendarsConstants.Features.Calendars,
            ["controller"] = controller,
            ["action"] = action,
        };

        return linkGenerator.GetPathByRouteValues(httpContext, null, routeValues);
    }

    /// <summary>
    /// Finds a calendar and the caller's access to it.
    /// </summary>
    /// <returns>The calendar and the access level, or a <see langword="null"/> calendar when it does not exist or the caller cannot see it.</returns>
    public static async Task<(Calendar Calendar, CalendarAccessLevel Level)> FindCalendarAsync(IServiceProvider services, ClaimsPrincipal user, string calendarId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(calendarId))
        {
            return (null, CalendarAccessLevel.None);
        }

        var calendar = await services.GetRequiredService<ICalendarService>().FindByIdAsync(calendarId.Trim(), cancellationToken);

        if (calendar is null)
        {
            return (null, CalendarAccessLevel.None);
        }

        var level = await services.GetRequiredService<ICalendarAccessService>().GetAccessLevelAsync(user, calendar, cancellationToken);

        return level == CalendarAccessLevel.None ? (null, CalendarAccessLevel.None) : (calendar, level);
    }

    /// <summary>
    /// Describes the errors the calendar services returned.
    /// </summary>
    public static string Describe(IEnumerable<CalendarError> errors)
        => string.Join(" ", (errors ?? []).Select(error => error.Message));
}
