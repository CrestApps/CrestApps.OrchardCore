using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Gives the user's own time-off balances.
/// </summary>
public sealed class TimeOffBalanceTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workforceTimeOffBalance";

    private static readonly JsonElement _schema = Schema("""{ "type": "object", "properties": {}, "additionalProperties": false }""");

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Gives the user's own time-off balances: what they have, what is booked, what waits for approval and what is available, per type.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var worker = await WorkerOfAsync(services, user);

        if (worker is null)
        {
            return "Your account isn't linked to a worker, so you have no time-off balances.";
        }

        var today = await PlanningOptions.TodayAsync(services.GetRequiredService<ILocalClock>());
        var balances = await services.GetRequiredService<ITimeOffBalanceService>().GetBalancesAsync(worker.ItemId, today, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            balances = balances.Select(balance => new
            {
                type = balance.Type?.Name,
                policy = balance.Policy?.Name,
                unit = balance.Type?.Unit.ToString(),
                current = balance.Current,
                booked = balance.Upcoming,
                pending = balance.Pending,
                available = balance.Available,
            }),
        }, JsonOptions);
    }
}
