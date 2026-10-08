using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the access screen: the grants for one child tenant, or for every child tenant.
/// </summary>
public class AccessGrantsViewModel
{
    /// <summary>
    /// Gets or sets the child tenant, or <see langword="null"/> for the grants that cover every child tenant.
    /// </summary>
    [BindNever]
    public ChildTenantInfo Child { get; set; }

    /// <summary>
    /// Gets or sets the grants shown.
    /// </summary>
    [BindNever]
    public List<AccessGrant> Grants { get; set; } = [];

    /// <summary>
    /// Gets or sets the grants for every child tenant, shown read-only on the screen of one child tenant.
    /// </summary>
    [BindNever]
    public List<AccessGrant> InheritedGrants { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles of the parent tenant.
    /// </summary>
    [BindNever]
    public List<string> ParentRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles that can be granted in the child tenant.
    /// </summary>
    [BindNever]
    public List<string> ChildRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    [BindNever]
    public HierarchyLabels Labels { get; set; }

    /// <summary>
    /// Gets or sets who the new grant applies to.
    /// </summary>
    public AccessGrantPrincipalType PrincipalType { get; set; } = AccessGrantPrincipalType.Role;

    /// <summary>
    /// Gets or sets the user name, email or role of the new grant.
    /// </summary>
    public string Principal { get; set; }

    /// <summary>
    /// Gets or sets the parent role of the new grant, when it applies to a role.
    /// </summary>
    public string PrincipalRole { get; set; }

    /// <summary>
    /// Gets or sets the child roles of the new grant.
    /// </summary>
    public List<string> SelectedChildRoles { get; set; } = [];
}
