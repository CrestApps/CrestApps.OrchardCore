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
/// Cancels or moves a customer's appointment.
/// </summary>
public sealed class ChangeAppointmentTool : SchedulingAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "changeAppointment";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentId": { "type": "string" },
        "proof": { "type": "string", "description": "The manage code from the booking, or the customer's email or phone number on the appointment." },
        "action": { "type": "string", "enum": ["cancel", "move"] },
        "newStartUtc": { "type": "string", "description": "For a move: the startUtc of an open time." }
      },
      "required": ["appointmentId", "proof", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Cancels or moves a customer's appointment. Confirm with the customer first.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var service = arguments.Services.GetRequiredService<IAppointmentService>();
        var appointment = await service.FindByIdAsync(Read(arguments, "appointmentId"), cancellationToken);
        var proof = Read(arguments, "proof");

        if (appointment is null || !(await CanManageAsync(arguments.Services) || Matches(appointment, proof)))
        {
            return "No appointment matches that id and proof.";
        }

        var result = Read(arguments, "action") == "move" && DateTimeOffset.TryParse(Read(arguments, "newStartUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start)
            ? await service.RescheduleAsync(appointment, start.UtcDateTime, actor: "AI", cancellationToken: cancellationToken)
            : Read(arguments, "action") == "cancel" ? await service.CancelAsync(appointment, "Cancelled through the AI assistant.", "AI", cancellationToken) : null;

        return result is null ? "Say cancel, or move with a newStartUtc." : result.Succeeded ? "Done." : "Not changed: " + string.Join(" ", result.Errors);
    }

    private static bool Matches(Appointment appointment, string proof)
    {
        return !string.IsNullOrEmpty(proof) &&
            (string.Equals(proof, appointment.ManageToken, StringComparison.Ordinal) ||
             string.Equals(proof, appointment.CustomerEmail, StringComparison.OrdinalIgnoreCase) ||
             (!string.IsNullOrEmpty(appointment.CustomerPhone) && new string(proof.Where(char.IsDigit).ToArray()) == new string(appointment.CustomerPhone.Where(char.IsDigit).ToArray())));
    }
}
