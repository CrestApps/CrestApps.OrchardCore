using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Clears a staff member's badge PIN through <see cref="IStaffBadgeService.ClearPin"/>, as the Badge card of the staff
/// editor does, or says whether one is set. A PIN is a secret, so the tool never takes or returns one: setting a new PIN
/// is done on the staff editor, whose address the result gives. Like the editor, it needs the person to be in the
/// caller's staff scope (<see cref="StaffPermissions.ManageStaff"/>, or <see cref="StaffPermissions.ManageOwnStaff"/> for
/// the people one manages). It asks for approval.
/// </summary>
internal sealed class SetStaffBadgePinTool : StaffManagementAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setStaffBadgePin";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "staffMemberId": {
          "type": "string",
          "description": "The id of the staff member, from getStaffMember."
        },
        "clearPin": {
          "type": "boolean",
          "description": "true removes the person's PIN, so they can't sign in at time clocks and registers until a new one is set on the staff editor. false or left out only says whether a PIN is set."
        }
      },
      "required": ["staffMemberId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Clears the PIN a staff member uses with their employee number at time clocks and registers, or says whether one is set. A PIN is never taken or returned: a new PIN is set on the staff editor, whose address the result gives.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => ManagesAnybodyAsync(services, caller, cancellationToken);

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var staffMemberId = ReadString(arguments, "staffMemberId");

        if (staffMemberId is null)
        {
            return "Nothing was changed: give the staffMemberId, from getStaffMember.";
        }

        var manager = services.GetRequiredService<IStaffMemberManager>();
        var staff = await manager.FindByIdAsync(staffMemberId, cancellationToken);

        if (staff is null)
        {
            return $"Nothing was changed: there is no staff member with the id '{staffMemberId}'.";
        }

        if (!await services.GetRequiredService<IStaffScopeService>().CanManageAsync(caller, staff, cancellationToken))
        {
            return $"Nothing was changed: you are not allowed to change {staff.DisplayName}'s badge: they are not among the people you manage.";
        }

        var badges = services.GetRequiredService<IStaffBadgeService>();
        var editorPath = GetEditorPath(services, staff.ItemId);
        var hadPin = badges.HasPin(staff);

        if (ReadBool(arguments, "clearPin") != true)
        {
            return Json(new
            {
                id = staff.ItemId,
                name = staff.DisplayName,
                hasPin = hadPin,
                editorPath,
                message = $"A new PIN is set on {staff.DisplayName}'s staff editor (Badge card), never through the assistant.",
            });
        }

        if (!hadPin)
        {
            return Json(new { id = staff.ItemId, name = staff.DisplayName, hasPin = false, cleared = false, editorPath, message = $"{staff.DisplayName} has no PIN to clear." });
        }

        badges.ClearPin(staff);

        var validation = await manager.ValidateAsync(staff, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was changed: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)))}";
        }

        await manager.UpdateAsync(staff, cancellationToken: cancellationToken);

        return Json(new
        {
            id = staff.ItemId,
            name = staff.DisplayName,
            hasPin = false,
            cleared = true,
            editorPath,
            message = $"{staff.DisplayName}'s PIN has been cleared. They can't sign in at time clocks and registers until a new PIN is set on their staff editor.",
        });
    }
}
