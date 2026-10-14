using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the create and edit screens of a child tenant.
/// </summary>
public class ChildTenantEditViewModel
{
    /// <summary>
    /// Gets or sets the registry entry, when a child tenant is edited.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Required]
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the slug.
    /// </summary>
    [Required]
    public string Slug { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the setup recipe, when a child tenant is created.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the setup recipes the policy allows.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Recipes { get; set; } = [];

    /// <summary>
    /// Gets or sets the descriptions of the setup recipes, by recipe name.
    /// </summary>
    [BindNever]
    public Dictionary<string, string> RecipeDescriptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the host pattern of child tenants, for the address preview.
    /// </summary>
    [BindNever]
    public string HostPattern { get; set; }

    /// <summary>
    /// Gets or sets the scheme of tenant addresses, for the address preview.
    /// </summary>
    [BindNever]
    public string Scheme { get; set; }

    /// <summary>
    /// Gets or sets the child tenant, when it is edited.
    /// </summary>
    [BindNever]
    public ChildTenantInfo Info { get; set; }

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    [BindNever]
    public HierarchyLabels Labels { get; set; }

    /// <summary>
    /// Gets or sets the number of child tenants the parent owns.
    /// </summary>
    [BindNever]
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the number of child tenants the policy allows.
    /// </summary>
    [BindNever]
    public int MaxChildren { get; set; }
}
