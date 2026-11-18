using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Deletes a staff record through the staff manager, as the staff list's Delete does: it needs
/// <see cref="StaffPermissions.ManageStaff"/> and is refused while people still report to the person. It asks for
/// approval.
/// </summary>
internal sealed class DeleteStaffMemberTool : StaffManagementAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteStaffMember";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "staffMemberId": {
          "type": "string",
          "description": "The id of the staff member to delete, from getStaffMember."
        }
      },
      "required": ["staffMemberId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a staff record. It is refused while people still report to them; set the status to Terminated instead to keep history.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => HasAsync(services, caller, StaffPermissions.ManageStaff);

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var staffMemberId = ReadString(arguments, "staffMemberId");

        if (staffMemberId is null)
        {
            return "Nothing was deleted: give the staffMemberId, from getStaffMember.";
        }

        var manager = services.GetRequiredService<IStaffMemberManager>();
        var staff = await manager.FindByIdAsync(staffMemberId, cancellationToken);

        if (staff is null)
        {
            return $"Nothing was deleted: there is no staff member with the id '{staffMemberId}'.";
        }

        var reports = await manager.ListDirectReportsAsync(staff.ItemId, cancellationToken);

        if (reports.Count > 0)
        {
            return Json(new
            {
                id = staff.ItemId,
                name = staff.DisplayName,
                deleted = false,
                reason = $"{staff.DisplayName} still has people reporting to them. Give them another manager first.",
                directReports = reports.Select(report => new { id = report.ItemId, name = report.DisplayName }),
            });
        }

        var deleted = await manager.DeleteAsync(staff, cancellationToken);

        return Json(new
        {
            id = staff.ItemId,
            name = staff.DisplayName,
            deleted,
            message = deleted ? $"{staff.DisplayName} has been deleted." : $"{staff.DisplayName} could not be deleted.",
        });
    }
}
