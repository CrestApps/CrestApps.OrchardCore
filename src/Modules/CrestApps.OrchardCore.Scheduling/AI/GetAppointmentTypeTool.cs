using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Gets an appointment type with every field of its editor.
/// </summary>
internal sealed class GetAppointmentTypeTool : SchedulingAdminToolBase
{
    public const string ToolName = "getAppointmentType";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentTypeId": { "type": "string", "description": "The appointment type's ID, from listAppointmentTypes." }
      },
      "required": ["appointmentTypeId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets an appointment type in full: timing, limits, notice rules, capacity, resource groups with their assignment rules, self-service and its public booking address.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ManageScheduling;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = SchedulingToolSupport.Read(arguments, "appointmentTypeId");
        var type = id is null ? null : await services.GetRequiredService<AppointmentTypeStore>().FindByIdAsync(id, cancellationToken);

        if (type is null)
        {
            return SchedulingToolResults.AppointmentTypeNotFound;
        }

        return SchedulingToolSupport.Json(await DescribeAsync(services, type, cancellationToken));
    }

    /// <summary>
    /// Describes an appointment type with the names of its resources and its links.
    /// </summary>
    internal static async Task<object> DescribeAsync(IServiceProvider services, AppointmentType type, CancellationToken cancellationToken)
    {
        var resources = (await services.GetRequiredService<SchedulingResourceStore>().GetAsync(type.GetAllResourceIds(), cancellationToken))
            .ToDictionary(resource => resource.ItemId, StringComparer.Ordinal);
        var bookingPath = string.IsNullOrEmpty(type.Slug) ? null : SchedulingToolSupport.GetRoutePath(services, "SchedulingBook", new { slug = type.Slug });
        var adminPath = SchedulingToolSupport.GetAdminPath(services, "AppointmentTypes", "Edit", new { id = type.ItemId });

        return SchedulingToolResults.AppointmentTypeDetail(type, resources, bookingPath, adminPath);
    }
}
