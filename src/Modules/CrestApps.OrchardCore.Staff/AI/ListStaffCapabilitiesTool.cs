using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Lists the capabilities through <see cref="IStaffCapabilityService.ListAsync"/>, built-in and custom, enabled or not,
/// as Staff &gt; Capabilities does. It needs <see cref="StaffPermissions.ViewStaff"/>, the permission of the staff list
/// whose capability filter offers them; the Capabilities page itself needs <see cref="StaffPermissions.ManageStaff"/>.
/// </summary>
internal sealed class ListStaffCapabilitiesTool : StaffManagementAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listStaffCapabilities";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "includeDisabled": {
          "type": "boolean",
          "description": "Whether capabilities an admin turned off are listed too. Defaults to true."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the kinds of work staff can be given (such as Technician, Agent, Cashier), built-in and custom, with whether each is enabled.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => HasAsync(services, caller, StaffPermissions.ViewStaff);

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var capabilities = await services.GetRequiredService<IStaffCapabilityService>().ListAsync(ReadBool(arguments, "includeDisabled") ?? true, cancellationToken);

        return Json(new
        {
            items = capabilities.Select(capability => new
            {
                name = capability.Name,
                displayName = capability.DisplayName,
                description = capability.Description,
                position = capability.Position,
                enabled = capability.Enabled,
                isBuiltIn = capability.IsBuiltIn,
                isCustomized = capability.IsCustomized,
            }),
            total = capabilities.Count,
            note = services.GetService<StaffCapabilityToolScope>()?.Note(),
        });
    }
}
