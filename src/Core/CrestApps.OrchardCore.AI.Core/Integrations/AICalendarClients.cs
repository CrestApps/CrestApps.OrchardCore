using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Core.Integrations;

/// <summary>
/// A busy stretch of a calendar.
/// </summary>
/// <param name="StartUtc">When it starts.</param>
/// <param name="EndUtc">When it ends.</param>
public sealed record AICalendarBusy(DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// An appointment to book.
/// </summary>
public sealed class AICalendarAppointment
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the notes.</summary>
    public string Notes { get; set; }

    /// <summary>Gets or sets when it starts.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Gets or sets when it ends.</summary>
    public DateTime EndUtc { get; set; }

    /// <summary>Gets or sets the customer's email address, invited when given.</summary>
    public string AttendeeEmail { get; set; }
}

/// <summary>
/// A booked appointment.
/// </summary>
/// <param name="Id">The event's id in the calendar.</param>
/// <param name="Link">A link to open it.</param>
public sealed record AICalendarEvent(string Id, string Link);

/// <summary>
/// A calendar the AI checks and books.
/// </summary>
public interface IAICalendarClient
{
    /// <summary>
    /// Lists the busy stretches between two times.
    /// </summary>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The busy stretches.</returns>
    Task<IReadOnlyList<AICalendarBusy>> GetBusyAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Books an appointment.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The booked event.</returns>
    Task<AICalendarEvent> BookAsync(AICalendarAppointment appointment, CancellationToken cancellationToken = default);
}

/// <summary>
/// Google Calendar, through its REST API (free/busy and event insert).
/// </summary>
public sealed class GoogleCalendarClient : IAICalendarClient
{
    private const string BaseUrl = "https://www.googleapis.com/calendar/v3/";

    private readonly HttpClient _httpClient;
    private readonly string _calendarId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleCalendarClient"/> class.
    /// </summary>
    /// <param name="httpClient">A client that carries the connection's token.</param>
    /// <param name="calendarId">The calendar; empty uses the primary one.</param>
    public GoogleCalendarClient(HttpClient httpClient, string calendarId)
    {
        _httpClient = httpClient;
        _calendarId = string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId.Trim();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AICalendarBusy>> GetBusyAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["timeMin"] = AICalendarSlots.Format(fromUtc),
            ["timeMax"] = AICalendarSlots.Format(toUtc),
            ["items"] = new JsonArray(new JsonObject { ["id"] = _calendarId }),
        };

        using var response = await _httpClient.PostAsJsonAsync(BaseUrl + "freeBusy", body, cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "Google Calendar", cancellationToken);
        var busy = new List<AICalendarBusy>();

        if (json.TryGetProperty("calendars", out var calendars))
        {
            foreach (var calendar in calendars.EnumerateObject())
            {
                if (calendar.Value.TryGetProperty("busy", out var periods))
                {
                    busy.AddRange(periods.EnumerateArray().Select(period => new AICalendarBusy(
                        AICalendarSlots.Parse(period.GetProperty("start").GetString()),
                        AICalendarSlots.Parse(period.GetProperty("end").GetString()))));
                }
            }
        }

        return busy;
    }

    /// <inheritdoc/>
    public async Task<AICalendarEvent> BookAsync(AICalendarAppointment appointment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var body = new JsonObject
        {
            ["summary"] = appointment.Title,
            ["description"] = appointment.Notes,
            ["start"] = new JsonObject { ["dateTime"] = AICalendarSlots.Format(appointment.StartUtc) },
            ["end"] = new JsonObject { ["dateTime"] = AICalendarSlots.Format(appointment.EndUtc) },
        };

        if (!string.IsNullOrWhiteSpace(appointment.AttendeeEmail))
        {
            body["attendees"] = new JsonArray(new JsonObject { ["email"] = appointment.AttendeeEmail.Trim() });
        }

        using var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}calendars/{Uri.EscapeDataString(_calendarId)}/events?sendUpdates=all", body, cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "Google Calendar", cancellationToken);

        return new AICalendarEvent(AIIntegrationHttp.String(json, "id"), AIIntegrationHttp.String(json, "htmlLink"));
    }
}

/// <summary>
/// An Outlook calendar in Microsoft 365, through Microsoft Graph (calendar view and event create).
/// </summary>
public sealed class MicrosoftCalendarClient : IAICalendarClient
{
    private const string BaseUrl = "https://graph.microsoft.com/v1.0/me/";

    private readonly HttpClient _httpClient;
    private readonly string _calendarPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="MicrosoftCalendarClient"/> class.
    /// </summary>
    /// <param name="httpClient">A client that carries the connection's token.</param>
    /// <param name="calendarId">The calendar; empty uses the default one.</param>
    public MicrosoftCalendarClient(HttpClient httpClient, string calendarId)
    {
        _httpClient = httpClient;
        _calendarPath = string.IsNullOrWhiteSpace(calendarId) ? string.Empty : $"calendars/{Uri.EscapeDataString(calendarId.Trim())}/";
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AICalendarBusy>> GetBusyAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{_calendarPath}calendarView?startDateTime={Uri.EscapeDataString(AICalendarSlots.Format(fromUtc))}&endDateTime={Uri.EscapeDataString(AICalendarSlots.Format(toUtc))}&$select=start,end,showAs&$top=250");

        // Times come back in UTC rather than in the mailbox's own zone.
        request.Headers.Add("Prefer", "outlook.timezone=\"UTC\"");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "Microsoft 365", cancellationToken);

        return json.TryGetProperty("value", out var events)
            ? events.EnumerateArray()
                .Where(item => AIIntegrationHttp.String(item, "showAs") is not "free")
                .Select(item => new AICalendarBusy(
                    AICalendarSlots.Parse(item.GetProperty("start").GetProperty("dateTime").GetString()),
                    AICalendarSlots.Parse(item.GetProperty("end").GetProperty("dateTime").GetString())))
                .ToList()
            : [];
    }

    /// <inheritdoc/>
    public async Task<AICalendarEvent> BookAsync(AICalendarAppointment appointment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var body = new JsonObject
        {
            ["subject"] = appointment.Title,
            ["body"] = new JsonObject { ["contentType"] = "text", ["content"] = appointment.Notes ?? string.Empty },
            ["start"] = new JsonObject { ["dateTime"] = AICalendarSlots.Format(appointment.StartUtc).TrimEnd('Z'), ["timeZone"] = "UTC" },
            ["end"] = new JsonObject { ["dateTime"] = AICalendarSlots.Format(appointment.EndUtc).TrimEnd('Z'), ["timeZone"] = "UTC" },
        };

        if (!string.IsNullOrWhiteSpace(appointment.AttendeeEmail))
        {
            body["attendees"] = new JsonArray(new JsonObject
            {
                ["emailAddress"] = new JsonObject { ["address"] = appointment.AttendeeEmail.Trim() },
                ["type"] = "required",
            });
        }

        using var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}{_calendarPath}events", body, cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "Microsoft 365", cancellationToken);

        return new AICalendarEvent(AIIntegrationHttp.String(json, "id"), AIIntegrationHttp.String(json, "webLink"));
    }
}

/// <summary>
/// Finds open appointment times from a calendar's busy stretches, inside working hours in the calendar's time zone.
/// </summary>
public static class AICalendarSlots
{
    /// <summary>
    /// Finds the open times.
    /// </summary>
    /// <param name="busy">The busy stretches.</param>
    /// <param name="fromUtc">The earliest start.</param>
    /// <param name="toUtc">The latest end.</param>
    /// <param name="duration">How long an appointment is.</param>
    /// <param name="timeZone">The calendar's time zone.</param>
    /// <param name="startHour">The first hour of the working day.</param>
    /// <param name="endHour">The hour the working day ends.</param>
    /// <param name="maxSlots">The most times to offer.</param>
    /// <returns>The open start times, in UTC.</returns>
    public static IReadOnlyList<DateTime> FindFreeSlots(
        IEnumerable<AICalendarBusy> busy,
        DateTime fromUtc,
        DateTime toUtc,
        TimeSpan duration,
        TimeZoneInfo timeZone,
        int startHour,
        int endHour,
        int maxSlots = 8)
    {
        var periods = (busy ?? []).OrderBy(period => period.StartUtc).ToList();
        var step = TimeSpan.FromMinutes(30);
        var slots = new List<DateTime>();
        timeZone ??= TimeZoneInfo.Utc;

        // Starts on the next half hour.
        var start = new DateTime(fromUtc.Ticks - (fromUtc.Ticks % step.Ticks), DateTimeKind.Utc);

        if (start < fromUtc)
        {
            start += step;
        }

        for (var candidate = start; candidate + duration <= toUtc && slots.Count < maxSlots; candidate += step)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(candidate, timeZone);
            var localEnd = local + duration;

            if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ||
                local.Hour < startHour ||
                localEnd.Date != local.Date ||
                localEnd.TimeOfDay > TimeSpan.FromHours(endHour))
            {
                continue;
            }

            var end = candidate + duration;

            if (!periods.Any(period => period.StartUtc < end && period.EndUtc > candidate))
            {
                slots.Add(candidate);
            }
        }

        return slots;
    }

    /// <summary>
    /// Writes a time the way the calendar APIs take it.
    /// </summary>
    /// <param name="utc">The time.</param>
    /// <returns>The ISO 8601 text.</returns>
    public static string Format(DateTime utc)
        => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a time a calendar API sent. A time with no zone is taken as UTC.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The time in UTC.</returns>
    public static DateTime Parse(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}

/// <summary>
/// Reads integration answers and turns refusals into errors that say which provider refused and why.
/// </summary>
public static class AIIntegrationHttp
{
    /// <summary>
    /// Reads a JSON answer.
    /// </summary>
    /// <param name="response">The answer.</param>
    /// <param name="provider">The provider's name, for the error.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JSON.</returns>
    /// <exception cref="HttpRequestException">The provider refused.</exception>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response, string provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{provider} answered {(int)response.StatusCode}: {(body.Length > 500 ? body[..500] : body)}", null, response.StatusCode);
        }

        return string.IsNullOrWhiteSpace(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>
    /// Reads a string property.
    /// </summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>The string, or <see langword="null"/>.</returns>
    public static string String(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
