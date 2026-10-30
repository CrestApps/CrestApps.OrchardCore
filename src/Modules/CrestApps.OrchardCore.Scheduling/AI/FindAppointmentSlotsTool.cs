using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Finds open times for an appointment type.
/// </summary>
public sealed class FindAppointmentSlotsTool : SchedulingAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "findAppointmentSlots";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentTypeId": { "type": "string" },
        "from": { "type": "string", "description": "ISO 8601 time with an offset." },
        "to": { "type": "string", "description": "ISO 8601 time with an offset. At most 14 days after from." }
      },
      "required": ["appointmentTypeId", "from", "to"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Finds open times for a kind of appointment. Offer the customer two or three of them.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var type = await FindTypeAsync(arguments.Services, Read(arguments, "appointmentTypeId"));

        if (type is null || !DateTimeOffset.TryParse(Read(arguments, "from"), out var from) || !DateTimeOffset.TryParse(Read(arguments, "to"), out var to))
        {
            return "Give a bookable appointmentTypeId and from and to as ISO 8601 times.";
        }

        var slots = await arguments.Services.GetRequiredService<ISchedulingAvailabilityService>().FindSlotsAsync(type, from.UtcDateTime, to > from.AddDays(14) ? from.UtcDateTime.AddDays(14) : to.UtcDateTime, maxResults: 20);
        var result = new List<object>();

        foreach (var slot in slots)
        {
            result.Add(new { start = await LocalAsync(arguments.Services, slot.StartUtc), startUtc = slot.StartUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) });
        }

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
