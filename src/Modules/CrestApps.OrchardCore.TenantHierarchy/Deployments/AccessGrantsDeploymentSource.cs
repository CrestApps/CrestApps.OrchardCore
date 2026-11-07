using System.Text.Json.Nodes;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.TenantHierarchy.Deployments;

/// <summary>
/// Writes the access grants into a <c>TenantHierarchyAccessGrants</c> recipe step: the grants for every child tenant
/// first, then each child tenant's, with the user or role by name and the child tenant by its entry id and slug.
/// </summary>
internal sealed class AccessGrantsDeploymentSource : DeploymentSourceBase<AccessGrantsDeploymentStep>
{
    private readonly AccessGrantStore _grantStore;
    private readonly ChildTenantEntryStore _entryStore;

    public AccessGrantsDeploymentSource(AccessGrantStore grantStore, ChildTenantEntryStore entryStore)
    {
        _grantStore = grantStore;
        _entryStore = entryStore;
    }

    protected override async Task ProcessAsync(AccessGrantsDeploymentStep step, DeploymentPlanResult result)
    {
        var grants = new JsonArray();

        foreach (var grant in await _grantStore.ListParentWideAsync())
        {
            grants.Add(AccessGrantsStep.Export(grant, null));
        }

        foreach (var entry in (await _entryStore.ListAsync()).OrderBy(entry => entry.Slug, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var grant in await _grantStore.ListForChildAsync(entry.EntryId))
            {
                grants.Add(AccessGrantsStep.Export(grant, entry));
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = TenantHierarchyConstants.Recipes.AccessGrants,
            [AccessGrantsStep.EntriesName] = grants,
        });
    }
}
