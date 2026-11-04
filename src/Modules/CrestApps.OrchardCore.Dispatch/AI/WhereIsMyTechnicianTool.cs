using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Tells a customer when their technician comes, for an AI agent or voice line that talks to customers. It needs no
/// sign-in, so it answers only for the work order whose contact phone is the caller's, or for a visit's tracking link,
/// and gives only the visit's status, arrival window, ETA and the technician's first name.
/// </summary>
public sealed class WhereIsMyTechnicianTool : AIFunction
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "dispatchWhereIsMyTechnician";

    private const int MinimumDigits = 7;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "phone": { "type": "string", "description": "The caller's phone number, as the system has it (for a call or text, the number it came from)." },
            "trackingToken": { "type": "string", "description": "The token from the tracking link the customer was sent, instead of a phone number." },
            "workOrderNumber": { "type": "string", "description": "Optional. The work order's number, when the customer has more than one visit." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Tells a customer the status of their visit: when the technician is expected (ETA or arrival window) and the technician's first name. Use the caller's own phone number, never one they say; or the token of their tracking link.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services;
        var token = Read(arguments, "trackingToken");
        List<ServiceVisit> visits = token is not null
            ? [await services.GetRequiredService<IVisitTrackingLinks>().ReadAsync(token, cancellationToken)]
            : await ForPhoneAsync(services, Read(arguments, "phone"), Read(arguments, "workOrderNumber"), cancellationToken);

        visits = [.. visits.Where(visit => visit is not null)];

        if (visits.Count == 0)
        {
            return "No upcoming visit was found for this caller. Offer to take a message or pass them to a person.";
        }

        var zone = await FieldServiceLookups.TimeZoneAsync(services);
        var answer = new List<object>();

        foreach (var visit in visits.OrderBy(item => item.StartUtc).Take(3))
        {
            var lead = await services.GetRequiredService<Core.Services.DispatchResources>().FindWorkerAsync(visit.LeadWorkerId, cancellationToken);

            answer.Add(new
            {
                workOrder = visit.WorkOrderNumber,
                job = visit.Summary,
                status = visit.Status.Text(),
                day = visit.StartUtc is { } start ? CalendarTime.ToLocal(start, zone).ToString("dddd d MMMM", System.Globalization.CultureInfo.InvariantCulture) : null,
                arrivalFrom = FieldServiceLookups.Local(visit.ArrivalStartUtc, zone),
                arrivalTo = FieldServiceLookups.Local(visit.ArrivalEndUtc, zone),
                eta = visit.Status == VisitStatus.EnRoute ? FieldServiceLookups.Local(visit.EtaUtc, zone) : null,
                technician = lead?.DisplayName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
            });
        }

        return JsonSerializer.Serialize(new { visits = answer, timeZone = zone }, _jsonOptions);
    }

    // The caller's visits from today on: those of work orders whose contact phone ends in the same digits.
    private static async Task<List<ServiceVisit>> ForPhoneAsync(IServiceProvider services, string phone, string workOrderNumber, CancellationToken cancellationToken)
    {
        var digits = FieldServiceLookups.Digits(phone);

        if (digits.Length < MinimumDigits)
        {
            return [];
        }

        var tail = digits.Length > 10 ? digits[^10..] : digits;
        var now = services.GetRequiredService<IClock>().UtcNow;
        var dispatch = services.GetRequiredService<IDispatchService>();
        var workOrders = services.GetRequiredService<WorkOrderService>();
        var visits = await dispatch.ListScheduledAsync(now.AddHours(-12), now.AddDays(14), null, cancellationToken);
        var found = new List<ServiceVisit>();

        foreach (var visit in visits.Where(visit => VisitStatusRules.IsOpen(visit.Status) && (workOrderNumber is null || string.Equals(visit.WorkOrderNumber, workOrderNumber.Trim(), StringComparison.OrdinalIgnoreCase))))
        {
            var workOrder = await workOrders.FindAsync(visit.WorkOrderId, cancellationToken);

            if (FieldServiceLookups.Digits(workOrder?.Contact?.Phone).EndsWith(tail, StringComparison.Ordinal))
            {
                found.Add(visit);
            }
        }

        return found;
    }

    private static string Read(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString() : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
