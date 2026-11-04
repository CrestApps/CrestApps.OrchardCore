using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Recipes;

/// <summary>
/// An access grant as the <c>TenantHierarchyAccessGrants</c> recipe step reads it.
/// </summary>
/// <param name="PrincipalType">Whether the grant is for a user or a role of the parent tenant.</param>
/// <param name="Principal">The user's name or email, or the role's name.</param>
/// <param name="ChildEntryId">The registry entry id of the child tenant, or <see langword="null"/>.</param>
/// <param name="ChildSlug">The slug of the child tenant, or <see langword="null"/>.</param>
/// <param name="ChildRoles">The roles the principal gets in the child tenant.</param>
internal sealed record AccessGrantRequest(
    AccessGrantPrincipalType PrincipalType,
    string Principal,
    string ChildEntryId,
    string ChildSlug,
    IReadOnlyList<string> ChildRoles);
