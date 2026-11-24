using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Argument reading, paging, JSON, times and admin links for the scheduling administration AI tools.
/// </summary>
internal static class SchedulingToolSupport
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    public const string ModuleArea = "CrestApps.OrchardCore.Scheduling";

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    public static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    public static bool Has(AIFunctionArguments arguments, string name)
        => arguments is not null && arguments.TryGetValue(name, out var value) && value is not null && value is not JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    /// <summary>
    /// Reads a text argument, trimmed; empty text is returned as empty, a missing argument as <see langword="null"/>.
    /// </summary>
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
    /// Reads a text argument; empty text is <see langword="null"/>.
    /// </summary>
    public static string Read(AIFunctionArguments arguments, string name)
    {
        var text = ReadRaw(arguments, name);

        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(ReadRaw(arguments, name), out var value) ? value : null;

    /// <summary>
    /// Reads a whole number, or <see langword="null"/> when it is missing or not a whole number.
    /// </summary>
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
    /// Reads a list of objects; <see langword="null"/> when it is missing or not a list of objects.
    /// </summary>
    public static IReadOnlyList<JsonElement> ReadObjects(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var element = ToElement(arguments[name]);

        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = element.EnumerateArray().ToList();

        return items.All(item => item.ValueKind == JsonValueKind.Object) ? items : null;
    }

    public static string GetText(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    /// <summary>
    /// Builds a page of a list result.
    /// </summary>
    public static object Paged<T>(IReadOnlyList<T> items, int total, int page, int pageSize, string timeZoneId = null)
        => new
        {
            items,
            total,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < total,
            timeZoneId,
        };

    /// <summary>
    /// Gets the site's time zone, as the scheduling pages use it.
    /// </summary>
    public static async Task<string> GetSiteTimeZoneIdAsync(IServiceProvider services)
        => CalendarTime.NormalizeZoneId((await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).TimeZoneId) ?? "UTC";

    /// <summary>
    /// Gets the time zone an appointment is shown in: its own when it has a valid one, otherwise the site's.
    /// </summary>
    public static string ZoneOf(string ownTimeZoneId, string siteTimeZoneId)
        => !string.IsNullOrWhiteSpace(ownTimeZoneId) && CalendarTime.IsValidZone(ownTimeZoneId) ? CalendarTime.NormalizeZoneId(ownTimeZoneId) : siteTimeZoneId;

    /// <summary>
    /// Formats a stored UTC time in a time zone, ISO 8601 with its offset.
    /// </summary>
    public static string Local(DateTime? utc, string timeZoneId)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var value = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        var local = CalendarTime.ToLocal(value, timeZoneId);
        var offset = DateTime.SpecifyKind(local, DateTimeKind.Unspecified) - DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        return new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), offset).ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats the day a stored UTC time falls on in a time zone, as yyyy-MM-dd.
    /// </summary>
    public static string LocalDay(DateTime utc, string timeZoneId)
        => CalendarTime.ToLocal(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZoneId).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the path of a scheduling admin page, or <see langword="null"/> when there is no request to build it from.
    /// </summary>
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
            ["area"] = ModuleArea,
            ["controller"] = controller,
            ["action"] = action,
        };

        return linkGenerator.GetPathByRouteValues(httpContext, null, routeValues);
    }

    /// <summary>
    /// Gets the path of a named route, or <see langword="null"/> when it is not there.
    /// </summary>
    public static string GetRoutePath(IServiceProvider services, string routeName, object values)
    {
        var httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services?.GetService<LinkGenerator>();

        return httpContext is null || linkGenerator is null ? null : linkGenerator.GetPathByName(httpContext, routeName, values);
    }

    private static JsonElement ToElement(object value)
        => value is JsonElement json ? json : JsonSerializer.SerializeToElement(value, _jsonOptions);
}
