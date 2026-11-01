using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The fields of a labor rule set and its rules.
/// </summary>
public class LaborRuleSetEditViewModel
{
    /// <summary>
    /// Gets or sets the set's id, or empty for a new set.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the teams it applies to.
    /// </summary>
    public string[] TeamIds { get; set; }

    /// <summary>
    /// Gets or sets the employment types it applies to.
    /// </summary>
    public string[] EmploymentTypes { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the rules.
    /// </summary>
    public IList<LaborRuleRowEditViewModel> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets the rule to add.
    /// </summary>
    public string AddRuleKey { get; set; }

    /// <summary>
    /// Gets or sets how many workers have the set on their own.
    /// </summary>
    [BindNever]
    public int WorkerCount { get; set; }

    /// <summary>
    /// Gets or sets the rules in the catalog, by key.
    /// </summary>
    [BindNever]
    public IReadOnlyDictionary<string, ILaborRule> Catalog { get; set; } = new Dictionary<string, ILaborRule>();

    /// <summary>
    /// Gets or sets the rules that can be added.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> RuleOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the employment types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> EmploymentOptions { get; set; } = [];
}

/// <summary>
/// One rule of a set being edited.
/// </summary>
public class LaborRuleRowEditViewModel
{
    /// <summary>
    /// Gets or sets the rule's id in the set.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the rule's key.
    /// </summary>
    public string RuleKey { get; set; }

    /// <summary>
    /// Gets or sets how it is enforced.
    /// </summary>
    public string Enforcement { get; set; }

    /// <summary>
    /// Gets or sets the parameters, by name.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
}
