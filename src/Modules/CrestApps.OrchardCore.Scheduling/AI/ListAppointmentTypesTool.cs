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
/// Lists the appointment types the AI can book.
/// </summary>
public sealed class ListAppointmentTypesTool : SchedulingAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "listAppointmentTypes";

    private static readonly JsonElement _schema = Schema("""{ "type": "object", "properties": {}, "additionalProperties": false }""");

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the kinds of appointments that can be booked, with their ids and how long each lasts.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var manage = await CanManageAsync(arguments.Services);
        var types = (await arguments.Services.GetRequiredService<AppointmentTypeStore>().ListAsync()).Where(type => type.Enabled && (type.AllowSelfService || manage));

        return JsonSerializer.Serialize(types.Select(type => new { id = type.ItemId, name = type.Name, description = type.Description, minutes = type.DurationMinutes, location = type.Location }), JsonOptions);
    }
}
