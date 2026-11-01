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
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Asks for the user's own time off.
/// </summary>
public sealed class RequestTimeOffTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workforceRequestTimeOff";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "typeName": { "type": "string", "description": "The kind of time off, such as Vacation." },
            "startDate": { "type": "string", "description": "The first day, as yyyy-MM-dd." },
            "endDate": { "type": "string", "description": "The last day, as yyyy-MM-dd; the first day when left out." },
            "startTime": { "type": "string", "description": "Optional. For part of one day, when it starts (HH:mm)." },
            "endTime": { "type": "string", "description": "Optional. For part of one day, when it ends (HH:mm)." },
            "reason": { "type": "string", "description": "Optional. Why." }
          },
          "required": [ "typeName", "startDate" ],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Asks for time off for the user. Confirm the type and the days with the user first. The request follows the same rules and approvals as one made in My Work.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, WorkforcePermissions.RequestTimeOff))
        {
            return "You aren't allowed to ask for time off here.";
        }

        var worker = await WorkerOfAsync(services, user);

        if (worker is null)
        {
            return "Your account isn't linked to a worker, so you can't ask for time off.";
        }

        var typeName = Read(arguments, "typeName");
        var types = (await services.GetRequiredService<ITimeOffTypeManager>().ListEnabledAsync(cancellationToken)).Where(type => type.VisibleToWorkers).ToList();
        var type = types.FirstOrDefault(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(typeName));

        if (type is null)
        {
            return $"There is no time-off type called '{typeName}'. The types are: {string.Join(", ", types.Select(item => item.Name))}.";
        }

        var start = ReadDate(arguments, "startDate");

        if (!start.HasValue)
        {
            return "Give the first day as yyyy-MM-dd.";
        }

        var input = new TimeOffRequestInput
        {
            WorkerId = worker.ItemId,
            TypeId = type.ItemId,
            StartDate = start.Value,
            EndDate = ReadDate(arguments, "endDate") ?? start.Value,
            StartTime = TimeOnly.TryParse(Read(arguments, "startTime"), System.Globalization.CultureInfo.InvariantCulture, out var startTime) ? startTime : null,
            EndTime = TimeOnly.TryParse(Read(arguments, "endTime"), System.Globalization.CultureInfo.InvariantCulture, out var endTime) ? endTime : null,
            Reason = Read(arguments, "reason"),
        };
        var result = await services.GetRequiredService<ITimeOffRequestService>().SubmitAsync(input, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return "The time off wasn't asked for: " + string.Join(" ", result.Errors);
        }

        return JsonSerializer.Serialize(new
        {
            type = type.Name,
            startDate = result.Value.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            endDate = result.Value.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            amount = result.Value.Amount,
            status = result.Value.Status.ToString(),
        }, JsonOptions);
    }
}
