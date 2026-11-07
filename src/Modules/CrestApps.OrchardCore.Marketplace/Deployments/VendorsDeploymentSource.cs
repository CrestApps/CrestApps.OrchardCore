using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Recipes;
using CrestApps.OrchardCore.Marketplace.Services;

namespace CrestApps.OrchardCore.Marketplace.Deployments;

/// <summary>
/// Writes every vendor into a <c>Vendors</c> recipe step, keyed by its id and its name, with its users by name. The
/// vendor's user ids and its Stripe Connect account, onboarding key and payout status belong to this site, so they are
/// left out.
/// </summary>
internal sealed class VendorsDeploymentSource : CatalogStoreDeploymentSource<VendorsDeploymentStep, Vendor>
{
    public VendorsDeploymentSource(IVendorStore store)
        : base(VendorsStep.EntriesName, store)
    {
    }

    protected override IEnumerable<Vendor> Order(IEnumerable<Vendor> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

    protected override JsonObject Export(Vendor entry)
    {
        var node = base.Export(entry);

        foreach (var member in VendorsStep.SiteOwnedMembers)
        {
            node.Remove(member);
        }

        return node;
    }
}
