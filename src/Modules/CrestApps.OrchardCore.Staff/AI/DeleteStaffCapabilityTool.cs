using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Deletes a custom capability, or resets a built-in one to its module's defaults, through
/// <see cref="IStaffCapabilityService.DeleteAsync"/>, as Staff &gt; Capabilities does. It needs
/// <see cref="StaffPermissions.ManageStaff"/> and asks for approval.
/// </summary>
internal sealed class DeleteStaffCapabilityTool : StaffManagementAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteStaffCapability";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The capability's key, from listStaffCapabilities."
        }
      },
      "required": ["name"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a custom capability, or resets a built-in one to its defaults. Staff who had a deleted capability keep it until they are next saved.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => HasAsync(services, caller, StaffPermissions.ManageStaff);

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var name = ReadString(arguments, "name");

        if (name is null)
        {
            return "Nothing was deleted: give the capability's name (its key), from listStaffCapabilities.";
        }

        var scope = services.GetService<StaffCapabilityToolScope>();

        if (scope?.Changed.Contains(name) == true)
        {
            return $"Nothing was deleted: the capability '{name}' was changed earlier in this message. Delete it in the next reply.";
        }

        var capabilities = services.GetRequiredService<IStaffCapabilityService>();
        var capability = await capabilities.FindAsync(name, cancellationToken);

        if (capability is null)
        {
            return $"Nothing was deleted: there is no capability '{name}'. Use listStaffCapabilities to find it.";
        }

        if (!await capabilities.DeleteAsync(capability.Name, cancellationToken))
        {
            return Json(new
            {
                name = capability.Name,
                deleted = false,
                message = capability.IsBuiltIn
                    ? $"'{capability.DisplayName}' already has its module's defaults; a built-in capability can't be deleted."
                    : $"'{capability.DisplayName}' could not be deleted.",
            });
        }

        scope?.Record(capability.Name);

        return Json(new
        {
            name = capability.Name,
            deleted = !capability.IsBuiltIn,
            reset = capability.IsBuiltIn,
            message = capability.IsBuiltIn
                ? $"'{capability.DisplayName}' is back to its defaults."
                : $"'{capability.DisplayName}' has been deleted. Staff members who had it keep it until they are saved again.",
        });
    }
}
