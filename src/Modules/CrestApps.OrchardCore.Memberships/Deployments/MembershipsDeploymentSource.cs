using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Recipes;
using CrestApps.OrchardCore.Memberships.Services;

namespace CrestApps.OrchardCore.Memberships.Deployments;

/// <summary>
/// Writes every membership into a <c>Memberships</c> recipe step, keyed by its id, with its member by user name. The
/// member's user id and the roles the membership put them in belong to this site, so they are left out.
/// </summary>
internal sealed class MembershipsDeploymentSource : CatalogStoreDeploymentSource<MembershipsDeploymentStep, Membership>
{
    public MembershipsDeploymentSource(IMembershipStore store)
        : base(MembershipsStep.EntriesName, store)
    {
    }

    protected override IEnumerable<Membership> Order(IEnumerable<Membership> entries)
        => entries.OrderBy(entry => entry.StartsUtc).ThenBy(entry => entry.UserName, StringComparer.OrdinalIgnoreCase);

    protected override JsonObject Export(Membership entry)
    {
        var node = base.Export(entry);

        foreach (var member in MembershipsStep.SiteOwnedMembers)
        {
            node.Remove(member);
        }

        return node;
    }
}
