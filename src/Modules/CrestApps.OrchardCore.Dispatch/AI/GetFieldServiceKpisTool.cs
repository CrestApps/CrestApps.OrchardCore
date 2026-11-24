using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Reports;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Gives the field service KPIs for a range of days, as the Field Service KPIs report page does.
/// </summary>
internal sealed class GetFieldServiceKpisTool : DispatchAdminToolBase
{
    public const string ToolName = "getFieldServiceKpis";

    // As the KPIs page: at most a year.
    private const int MaxDays = 366;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "from": { "type": "string", "description": "Optional. The first day (yyyy-MM-dd, in the dispatch time zone); 29 days before 'to' by default." },
        "to": { "type": "string", "description": "Optional. The last day (yyyy-MM-dd, in the dispatch time zone); today by default." },
        "repeatDays": { "type": "integer", "minimum": 1, "maximum": 365, "description": "Optional. How many days after a finished job a new work order for the same place counts as repeat work; 30 by default." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gives the field-service KPIs for a date range: finished visits, first-time fix, on-time arrival, travel and on-site time, jobs per technician day, repeat work, SLA compliance, hours to repair, revenue per work order and parts used.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => DispatchPermissions.ViewDispatch;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var zone = await DispatchAdminTools.TimeZoneAsync(services);
        var today = DateOnly.FromDateTime(CalendarTime.ToLocal(services.GetRequiredService<IClock>().UtcNow, zone));
        var to = today;
        DateOnly? from = null;

        if (DispatchAdminTools.Has(arguments, "to"))
        {
            if (DispatchAdminTools.ReadDate(arguments, "to") is not { } last)
            {
                return "Give 'to' as yyyy-MM-dd.";
            }

            to = last;
        }

        if (DispatchAdminTools.Has(arguments, "from"))
        {
            if (DispatchAdminTools.ReadDate(arguments, "from") is not { } first)
            {
                return "Give 'from' as yyyy-MM-dd.";
            }

            if (first > to)
            {
                return "'from' must not be after 'to'.";
            }

            from = first;
        }

        var start = from ?? to.AddDays(-29);
        var shortened = false;

        if (to.DayNumber - start.DayNumber >= MaxDays)
        {
            start = to.AddDays(-(MaxDays - 1));
            shortened = true;
        }

        var repeatDays = Math.Clamp(DispatchAdminTools.ReadInt(arguments, "repeatDays") ?? FieldServiceKpiService.DefaultRepeatDays, 1, 365);
        var result = await services.GetRequiredService<FieldServiceKpiService>().GetAsync(
            CalendarTime.ToUtc(start.ToDateTime(TimeOnly.MinValue), zone),
            CalendarTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone),
            zone,
            repeatDays,
            cancellationToken);

        return DispatchAdminTools.Json(new
        {
            from = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            to = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            timeZone = zone,
            rangeShortened = shortened ? $"The range was shortened to the last {MaxDays} days, as on the KPIs page." : null,
            finishedVisits = result.FinishedVisits,
            firstTimeFixRate = Rate(result.FirstTimeFixRate),
            onTimeArrivalRate = Rate(result.OnTimeArrivalRate),
            travelMinutes = result.TravelMinutes,
            onSiteMinutes = result.OnSiteMinutes,
            jobsPerTechnicianDay = Round(result.JobsPerTechnicianDay),
            returnVisitRate = Rate(result.ReturnVisitRate),
            repeatDays = result.RepeatDays,
            repeatWorkRate = Rate(result.RepeatWorkRate),
            workOrders = result.WorkOrders,
            responseCompliance = Rate(result.ResponseCompliance),
            resolutionCompliance = Rate(result.ResolutionCompliance),
            meanHoursToRepair = Round(result.MeanHoursToRepair),
            revenuePerWorkOrder = result.RevenuePerWorkOrder,
            revenueCurrency = result.RevenueCurrency,
            partsByLocation = result.PartsByLocation.Select(part => new { locationId = part.LocationId, name = part.Name, quantity = part.Quantity, workOrders = part.WorkOrders }).ToArray(),
            truncated = result.Truncated,
            note = "Rates are fractions from 0 to 1; a null rate means there was nothing to measure.",
        });
    }

    private static double? Rate(double? value)
        => value is { } rate ? Math.Round(rate, 4) : null;

    private static double? Round(double? value)
        => value is { } number ? Math.Round(number, 2) : null;
}
