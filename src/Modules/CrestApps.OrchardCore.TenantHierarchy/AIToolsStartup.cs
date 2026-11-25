using CrestApps.Core.AI;
using CrestApps.OrchardCore.TenantHierarchy.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy;

/// <summary>
/// Registers the child tenant AI tools of a parent tenant whenever the AI feature is on. They work only in a parent
/// tenant and check the permission of the child tenants page they mirror. Every tool that creates, sets up, changes or
/// removes a child tenant, or decides who may enter one, asks for approval.
/// </summary>
[Feature(TenantHierarchyConstants.Features.Parent)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ParentAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ParentAIToolsStartup(IStringLocalizer<ParentAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListChildTenantsTool>(ListChildTenantsTool.TheName)
            .WithTitle(S["List Child Tenants"])
            .WithDescription(S["Lists this parent tenant's child tenants with entry id, name, address, state, description and creation date; filter by text and state, and optionally include the limits and setup recipes the platform allows. Call it first: every other child tenant tool needs the entry id."])
            .WithCategory(S["Tenant Hierarchy"])
            .Selectable();

        services.AddCoreAITool<SaveChildTenantTool>(SaveChildTenantTool.TheName)
            .WithTitle(S["Save Child Tenant"])
            .WithDescription(S["Creates a child tenant (name, slug, description, setup recipe) and starts its setup, or changes the name, slug and description of an existing one. Creating provisions a database and fails when the policy's child limit is reached or the address is taken."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<ChangeChildTenantStateTool>(ChangeChildTenantStateTool.TheName)
            .WithTitle(S["Change Child Tenant State"])
            .WithDescription(S["Suspends, resumes or reloads one or more child tenants. Suspending takes the site offline for its users; a tenant pending removal must be restored before it can be resumed."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<RemoveChildTenantTool>(RemoveChildTenantTool.TheName)
            .WithTitle(S["Remove Child Tenant"])
            .WithDescription(S["Removes a suspended child tenant and its database, at once or after the policy's grace period. Requires the tenant's exact display name as confirmation."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<RestoreChildTenantTool>(RestoreChildTenantTool.TheName)
            .WithTitle(S["Restore Child Tenant"])
            .WithDescription(S["Cancels the pending removal of a child tenant during its grace period. The tenant stays suspended until resumed."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<RetryChildTenantSetupTool>(RetryChildTenantSetupTool.TheName)
            .WithTitle(S["Retry Child Tenant Setup"])
            .WithDescription(S["Creates a child tenant whose setup failed again, with the same name, address and recipe, and runs the setup. Use it only on entries in the Failed state."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<DiscardChildTenantTool>(DiscardChildTenantTool.TheName)
            .WithTitle(S["Discard Child Tenant"])
            .WithDescription(S["Deletes the leftovers of a child tenant that cannot be used: discards a failed setup with its registry entry, or dismisses the entry of a tenant the platform removed or moved."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<ListChildTenantFeaturesTool>(ListChildTenantFeaturesTool.TheName)
            .WithTitle(S["List Child Tenant Features"])
            .WithDescription(S["Lists the features a running child tenant may enable, by category, with whether each is on."])
            .WithCategory(S["Tenant Hierarchy"])
            .Selectable();

        services.AddCoreAITool<SetChildTenantFeaturesTool>(SetChildTenantFeaturesTool.TheName)
            .WithTitle(S["Set Child Tenant Features"])
            .WithDescription(S["Enables or disables features in a child tenant. Blocked and always-on features are ignored; the tenant must be running."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<ListChildTenantAccessGrantsTool>(ListChildTenantAccessGrantsTool.TheName)
            .WithTitle(S["List Child Tenant Access Grants"])
            .WithDescription(S["Lists who may enter child tenants: the grants for all children or for one child (with the inherited ones), plus the parent roles and child roles a grant can use."])
            .WithCategory(S["Tenant Hierarchy"])
            .Selectable();

        services.AddCoreAITool<GrantChildTenantAccessTool>(GrantChildTenantAccessTool.TheName)
            .WithTitle(S["Grant Child Tenant Access"])
            .WithDescription(S["Lets a parent user (by user name or email) or a parent role enter one child tenant or all of them, with the given child roles."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<RevokeChildTenantAccessTool>(RevokeChildTenantAccessTool.TheName)
            .WithTitle(S["Revoke Child Tenant Access"])
            .WithDescription(S["Removes an access grant by its id. Open sessions end at their next validation."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();
    }
}

/// <summary>
/// Registers the platform AI tools of the Default tenant whenever the AI feature is on. They need
/// <c>ManageTenantHierarchy</c>, as every action of the Tenant Hierarchy page does, and every tool that changes the
/// hierarchy asks for approval.
/// </summary>
[Feature(TenantHierarchyConstants.Features.Platform)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class PlatformAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PlatformAIToolsStartup(IStringLocalizer<PlatformAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetTenantHierarchyOverviewTool>(GetTenantHierarchyOverviewTool.TheName)
            .WithTitle(S["Get Tenant Hierarchy Overview"])
            .WithDescription(S["Shows the tenant hierarchy from the Default tenant: parent tenants with their children, orphaned children, tenants that can become parents, the database pools and whether the host guard is installed. Pass a parent to get its policy and children."])
            .WithCategory(S["Tenant Hierarchy"])
            .Selectable();

        services.AddCoreAITool<SaveParentTenantTool>(SaveParentTenantTool.TheName)
            .WithTitle(S["Save Parent Tenant"])
            .WithDescription(S["Makes an ordinary tenant a parent tenant (slug, display name, policy) or changes the display name and policy of an existing parent. Making a parent changes the tenant's host and forces the Parent feature on; it fails until AddTenantHierarchy() is in the host."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<ChangeParentTenantStateTool>(ChangeParentTenantStateTool.TheName)
            .WithTitle(S["Change Parent Tenant State"])
            .WithDescription(S["Suspends a parent tenant with all its running children, or resumes it with exactly the children suspended with it."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<UnmakeParentTenantTool>(UnmakeParentTenantTool.TheName)
            .WithTitle(S["Unmake Parent Tenant"])
            .WithDescription(S["Turns a parent tenant back into an ordinary tenant. Only works when it has no children left."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<RemoveParentTenantTool>(RemoveParentTenantTool.TheName)
            .WithTitle(S["Remove Parent Tenant"])
            .WithDescription(S["Removes a suspended parent tenant and all its child tenants with their data. Requires the parent's exact tenant name as confirmation."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<MoveChildTenantTool>(MoveChildTenantTool.TheName)
            .WithTitle(S["Move Child Tenant"])
            .WithDescription(S["Moves a child tenant to another parent, or gives an orphaned child a parent. Its address follows the new parent's pattern and sessions from the old parent end."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<DetachChildTenantTool>(DetachChildTenantTool.TheName)
            .WithTitle(S["Detach Child Tenant"])
            .WithDescription(S["Makes an orphaned child tenant an ordinary tenant that no parent manages."])
            .WithCategory(S["Tenant Hierarchy"])
            .AsSecurity()
            .Selectable();
    }
}
