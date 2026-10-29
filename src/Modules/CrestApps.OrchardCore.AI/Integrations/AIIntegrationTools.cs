using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Integrations;

/// <summary>
/// The integration connections an AI profile's tools use.
/// </summary>
public sealed class AIProfileIntegrationSettings
{
    /// <summary>
    /// Gets or sets the calendar connection the profile checks and books.
    /// </summary>
    public string CalendarConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the CRM connection the profile looks contacts up in.
    /// </summary>
    public string CrmConnectionId { get; set; }

    /// <summary>
    /// Gets or sets how long an appointment is, in minutes.
    /// </summary>
    public int AppointmentMinutes { get; set; } = 30;
}

/// <summary>
/// The base of the integration tools: finds the conversation's profile and the connection it uses.
/// </summary>
public abstract class AIIntegrationTool : AIFunction
{
    private readonly JsonElement _jsonSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationTool"/> class.
    /// </summary>
    /// <param name="properties">The JSON of the schema's properties.</param>
    /// <param name="required">The required properties.</param>
    protected AIIntegrationTool(string properties, string required = "")
    {
        _jsonSchema = JsonSerializer.Deserialize<JsonElement>($$"""{"type":"object","properties":{{{properties}}},"required":[{{required}}],"additionalProperties":false}""");
    }

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object> { ["Strict"] = false };

    /// <summary>
    /// Finds the connection the conversation's profile uses.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="pick">Picks the connection id from the profile's settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection and the settings, or an explanation for the AI.</returns>
    protected static async Task<(AIIntegrationConnection Connection, AIProfileIntegrationSettings Settings, string Problem)> FindConnectionAsync(
        IServiceProvider services,
        Func<AIProfileIntegrationSettings, string> pick,
        CancellationToken cancellationToken)
    {
        var profile = AIConversationScope.Current?.Profile;
        var settings = profile?.GetSettings<AIProfileIntegrationSettings>() ?? new AIProfileIntegrationSettings();
        var id = pick(settings);

        if (string.IsNullOrEmpty(id))
        {
            return (null, settings, "No connection is set up for this. Tell the customer a person will follow up.");
        }

        var connection = await services.GetRequiredService<IAIIntegrationConnectionStore>().FindByIdAsync(id, cancellationToken);

        return connection is null || !connection.IsConnected
            ? (null, settings, "The connection is not signed in. Tell the customer a person will follow up.")
            : (connection, settings, null);
    }

    /// <summary>
    /// Gets a connection's time zone.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The time zone, or UTC.</returns>
    protected static TimeZoneInfo TimeZoneOf(AIIntegrationConnection connection)
        => !string.IsNullOrWhiteSpace(connection?.TimeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(connection.TimeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
}

/// <summary>
/// Lists open appointment times in the profile's calendar.
/// </summary>
public sealed class CheckCalendarAvailabilityTool : AIIntegrationTool
{
    public const string TheName = "checkCalendarAvailability";

    public CheckCalendarAvailabilityTool()
        : base("""
            "fromDate": { "type": "string", "description": "The first day to look at, as yyyy-MM-dd in the business's time zone. Defaults to today." },
            "days": { "type": "integer", "description": "How many days to look at, 1 to 14. Defaults to 3." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Lists open appointment times in the business's calendar, inside working hours. Offer the customer two or three of them, then book the one they pick with bookCalendarAppointment.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var (connection, settings, problem) = await FindConnectionAsync(arguments.Services, settings => settings.CalendarConnectionId, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        var zone = TimeZoneOf(connection);
        var now = arguments.Services.GetRequiredService<IClock>().UtcNow;
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(now, zone).Date;
        var firstDay = arguments.TryGetFirstString("fromDate", out var text) && DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date >= localToday ? date : localToday;
        var days = arguments.TryGetFirstString("days", out var daysText) && int.TryParse(daysText, out var count) ? Math.Clamp(count, 1, 14) : 3;
        var from = Max(now, TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(firstDay, DateTimeKind.Unspecified), zone));
        var to = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(firstDay.AddDays(days), DateTimeKind.Unspecified), zone);

        var calendar = await arguments.Services.GetRequiredService<AIIntegrationClients>().CreateCalendarAsync(connection, cancellationToken);
        var busy = await calendar.GetBusyAsync(from, to, cancellationToken);
        var slots = AICalendarSlots.FindFreeSlots(busy, from, to, TimeSpan.FromMinutes(Math.Clamp(settings.AppointmentMinutes, 10, 240)), zone, connection.WorkdayStartHour, connection.WorkdayEndHour);

        if (slots.Count == 0)
        {
            return "There are no open times in those days. Offer to look at later days.";
        }

        return JsonSerializer.Serialize(new
        {
            TimeZone = zone.Id,
            DurationMinutes = settings.AppointmentMinutes,
            Slots = slots.Select(slot => new
            {
                StartUtc = AICalendarSlots.Format(slot),
                Spoken = TimeZoneInfo.ConvertTimeFromUtc(slot, zone).ToString("dddd MMMM d 'at' h:mm tt", CultureInfo.InvariantCulture),
            }),
        });
    }

    private static DateTime Max(DateTime a, DateTime b)
        => a > b ? a : b;
}

/// <summary>
/// Books an appointment in the profile's calendar.
/// </summary>
public sealed class BookCalendarAppointmentTool : AIIntegrationTool
{
    public const string TheName = "bookCalendarAppointment";

    public BookCalendarAppointmentTool()
        : base("""
            "startUtc": { "type": "string", "description": "The start, exactly as checkCalendarAvailability gave it." },
            "title": { "type": "string", "description": "What the appointment is for, such as 'Consultation with Jane Doe'." },
            "attendeeEmail": { "type": "string", "description": "The customer's email address, to send them an invitation. Optional." },
            "notes": { "type": "string", "description": "Anything the business should know before the appointment." }
            """, "\"startUtc\",\"title\"")
    {
    }

    public override string Name => TheName;

    public override string Description => "Books an appointment in the business's calendar at a time checkCalendarAvailability offered. Confirm the time with the customer before booking.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var (connection, settings, problem) = await FindConnectionAsync(arguments.Services, settings => settings.CalendarConnectionId, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        if (!arguments.TryGetFirstString("startUtc", out var startText) || !DateTime.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var start))
        {
            return "Give startUtc exactly as checkCalendarAvailability gave it.";
        }

        var end = start.AddMinutes(Math.Clamp(settings.AppointmentMinutes, 10, 240));
        var calendar = await arguments.Services.GetRequiredService<AIIntegrationClients>().CreateCalendarAsync(connection, cancellationToken);

        // Taken since it was offered: say so rather than double-book.
        if ((await calendar.GetBusyAsync(start, end, cancellationToken)).Any(period => period.StartUtc < end && period.EndUtc > start))
        {
            return "That time was just taken. Check availability again and offer other times.";
        }

        arguments.TryGetFirstString("title", out var title);
        arguments.TryGetFirstString("attendeeEmail", out var email);
        arguments.TryGetFirstString("notes", out var notes);
        var context = AIConversationScope.Current;

        var booked = await calendar.BookAsync(new AICalendarAppointment
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Appointment" : title,
            Notes = string.Join("\n", new[] { notes, string.IsNullOrEmpty(context?.CustomerAddress) ? null : $"Customer: {context.CustomerAddress}" }.Where(line => !string.IsNullOrWhiteSpace(line))),
            StartUtc = start,
            EndUtc = end,
            AttendeeEmail = email,
        }, cancellationToken);

        context?.Session?.SetConversationVariables([new("appointment_start_utc", AICalendarSlots.Format(start)), new("appointment_event_id", booked.Id ?? string.Empty)]);

        return JsonSerializer.Serialize(new { Booked = true, StartUtc = AICalendarSlots.Format(start), EventId = booked.Id });
    }
}

/// <summary>
/// Looks the caller up in the profile's CRM.
/// </summary>
public sealed class FindCrmContactTool : AIIntegrationTool
{
    public const string TheName = "findCrmContact";

    public FindCrmContactTool()
        : base("""
            "phone": { "type": "string", "description": "The phone number to look up, in E.164 form. Defaults to the number the customer is calling from." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Looks the customer up in the business's CRM by phone number and returns their name and email when found.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var (connection, _, problem) = await FindConnectionAsync(arguments.Services, settings => settings.CrmConnectionId, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        var phone = arguments.TryGetFirstString("phone", out var given) && !string.IsNullOrWhiteSpace(given) ? given : AIConversationScope.Current?.CustomerAddress;

        if (string.IsNullOrWhiteSpace(phone))
        {
            return "There is no phone number to look up.";
        }

        var crm = await arguments.Services.GetRequiredService<AIIntegrationClients>().CreateCrmAsync(connection, cancellationToken);
        var contact = await crm.FindByPhoneAsync(phone, cancellationToken);

        return contact is null
            ? "The customer is not in the CRM."
            : JsonSerializer.Serialize(new { Found = true, contact.FirstName, contact.LastName, contact.Email });
    }
}
