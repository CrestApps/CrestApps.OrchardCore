using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Memberships.AI;

/// <summary>
/// Ends an active membership now, as End now on Commerce › Memberships does
/// (<see cref="MembershipService.EndAsync"/> with <see cref="MembershipStatus.Revoked"/>): the member is taken out of
/// the roles it put them in that nothing else still gives them. Like the page, it refuses a membership that is not
/// active, and it refunds nothing.
/// </summary>
internal sealed class EndMembershipTool : MembershipsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "endMembership";

    private const int MaxReasonLength = 500;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "membershipId": {
          "type": "string",
          "description": "The membership's id, from listMemberships."
        },
        "reason": {
          "type": "string",
          "description": "Why the membership is ended, kept with it after the name of the person who ended it."
        }
      },
      "required": ["membershipId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Ends an active membership now and removes the member from the roles it added. Refunds nothing and cannot be undone from the admin; the member must buy again.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => MembershipsPermissions.ManageMemberships;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "membershipId") is not { } membershipId)
        {
            return "Nothing was ended: give the membership's id. Use listMemberships to find it.";
        }

        var reason = ReadString(arguments, "reason");

        if (reason?.Length > MaxReasonLength)
        {
            return $"Nothing was ended: the reason can be up to {MaxReasonLength} characters.";
        }

        var membership = await services.GetRequiredService<IMembershipStore>().FindByIdAsync(membershipId, cancellationToken);

        if (membership is null)
        {
            return $"Nothing was ended: no membership has the id '{membershipId}'. Use listMemberships to find it.";
        }

        if (membership.Status != MembershipStatus.Active)
        {
            return $"Nothing was ended: the membership of {membership.UserName} to {membership.Title} is {ListMembershipsTool.DescribeStatus(membership.Status)}, and only an active membership can be ended.";
        }

        var actor = caller.Identity?.Name;
        var recorded = reason is null ? $"Ended by {actor}." : $"Ended by {actor}: {reason}";

        await services.GetRequiredService<MembershipService>().EndAsync(membership, MembershipStatus.Revoked, recorded, cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        return Json(new
        {
            membership = await ListMembershipsTool.DescribeAsync(services, membership),
            rolesRemoved = membership.RoleNames ?? [],
            refunded = false,
            message = $"The membership of {membership.UserName} to {membership.Title} was ended, and the member was taken out of the roles it gave that nothing else still gives them. Nothing was refunded; refund the order separately if needed.",
        });
    }
}
