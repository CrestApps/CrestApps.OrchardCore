using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Memberships.AI;

/// <summary>
/// Lists memberships as Commerce › Memberships does (<see cref="IMembershipStore.PageAsync"/>): newest first, with the
/// page's search on the member's user name and its status filter.
/// </summary>
internal sealed class ListMembershipsTool : MembershipsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listMemberships";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "The start of the member's user name, as the page's search."
        },
        "status": {
          "type": "string",
          "enum": ["active", "expired", "endedEarly"],
          "description": "Only memberships with this status: active, expired (its end date passed) or endedEarly (ended by the store or a refund). Leave it out for every membership."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many memberships per page, up to 50 (default 20)."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists memberships with optional member search, status and paging: member, product, roles, start and end dates, orders. Use it to see who is a member or to find a membership's id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => MembershipsPermissions.ManageMemberships;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        MembershipStatus? status = ReadString(arguments, "status") switch
        {
            null => null,
            var text when string.Equals(text, "active", StringComparison.OrdinalIgnoreCase) => MembershipStatus.Active,
            var text when string.Equals(text, "expired", StringComparison.OrdinalIgnoreCase) => MembershipStatus.Expired,
            var text when string.Equals(text, "endedEarly", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "revoked", StringComparison.OrdinalIgnoreCase) => MembershipStatus.Revoked,
            _ => (MembershipStatus)(-1),
        };

        if (status == (MembershipStatus)(-1))
        {
            return "Nothing was listed: status must be active, expired or endedEarly.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (count, memberships) = await services.GetRequiredService<IMembershipStore>()
            .PageAsync(ReadString(arguments, "search"), status, (page - 1) * pageSize, pageSize, cancellationToken);

        var items = new List<object>();

        foreach (var membership in memberships)
        {
            items.Add(await DescribeAsync(services, membership));
        }

        return ListResult(items, count, page, pageSize);
    }

    /// <summary>
    /// Describes a membership from an explicit list of fields.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="membership">The membership.</param>
    /// <returns>The membership's fields.</returns>
    internal static async Task<object> DescribeAsync(IServiceProvider services, Membership membership)
        => new
        {
            id = membership.ItemId,
            member = membership.UserName,
            userId = membership.UserId,
            product = membership.Title,
            productId = membership.ContentItemId,
            roles = membership.RoleNames ?? [],
            status = DescribeStatus(membership.Status),
            startsAt = await LocalTimeAsync(services, membership.StartsUtc),
            endsAt = await LocalTimeAsync(services, membership.EndsUtc),
            endedReason = membership.EndedReason,
            orderIds = membership.OrderIds ?? [],
        };

    /// <summary>
    /// Names a status as the tools and the page do: an ended-early membership is stored as revoked.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The name.</returns>
    internal static string DescribeStatus(MembershipStatus status)
        => status switch
        {
            MembershipStatus.Active => "active",
            MembershipStatus.Expired => "expired",
            _ => "endedEarly",
        };
}
