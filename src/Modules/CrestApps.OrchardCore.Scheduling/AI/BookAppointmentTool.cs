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
/// Books an appointment for the customer.
/// </summary>
public sealed class BookAppointmentTool : SchedulingAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "bookAppointment";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentTypeId": { "type": "string" },
        "startUtc": { "type": "string", "description": "The startUtc findAppointmentSlots returned." },
        "customerName": { "type": "string" },
        "customerEmail": { "type": "string" },
        "customerPhone": { "type": "string" },
        "notes": { "type": "string" }
      },
      "required": ["appointmentTypeId", "startUtc", "customerName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Books an appointment at an open time. Read the time back to the customer and get a clear yes first. If the result says it waits for confirmation, tell the customer it is requested, not booked.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var type = await FindTypeAsync(arguments.Services, Read(arguments, "appointmentTypeId"));

        if (type is null || !DateTimeOffset.TryParse(Read(arguments, "startUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start))
        {
            return "Give a bookable appointmentTypeId and the startUtc of an open time.";
        }

        var result = await arguments.Services.GetRequiredService<IAppointmentService>().BookAsync(new AppointmentRequest
        {
            AppointmentTypeId = type.ItemId,
            StartUtc = start.UtcDateTime,
            CustomerName = Read(arguments, "customerName"),
            CustomerEmail = Read(arguments, "customerEmail"),
            CustomerPhone = Read(arguments, "customerPhone"),
            Notes = Read(arguments, "notes"),
            Source = "AI",
            Actor = "AI",
            SelfService = !await CanManageAsync(arguments.Services),
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return "Not booked: " + string.Join(" ", result.Errors);
        }

        var resources = await arguments.Services.GetRequiredService<SchedulingResourceStore>().GetAsync(result.Appointment.ResourceIds, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            appointmentId = result.Appointment.ItemId,
            with = result.Appointment.ResourceIds.Select(id => resources.FirstOrDefault(resource => resource.ItemId == id)?.Name).Where(name => name is not null).ToList(),
            manageCode = result.Appointment.ManageToken,
            status = result.Appointment.Status == AppointmentStatus.Requested ? "Requested: waits for confirmation" : result.Appointment.Status.ToString(),
            start = await LocalAsync(arguments.Services, result.Appointment.StartUtc),
        }, JsonOptions);
    }
}
