using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// The editor of a credential definition.
/// </summary>
public class CredentialDefinitionPartViewModel
{
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the criteria narrative.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the achievement type.
    /// </summary>
    public string AchievementType { get; set; }

    /// <summary>
    /// Gets or sets the tags, separated by commas.
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Gets or sets how many months an award stays valid; empty when it does not expire.
    /// </summary>
    public int? ValidityMonths { get; set; }

    /// <summary>
    /// Gets or sets the id of the aligned Skills skill.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets the id of the aligned Skills certification.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the award rules.
    /// </summary>
    public List<CredentialRuleRow> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets the alignments.
    /// </summary>
    public List<CredentialAlignmentRow> Alignments { get; set; } = [];

    /// <summary>
    /// Gets or sets the achievement types offered.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AchievementTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the rules offered: each kind with its targets, as <c>kind|target</c>.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> RuleOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the skills offered, when Skills is on.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Skills { get; set; } = [];

    /// <summary>
    /// Gets or sets the certifications offered, when Skills is on.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Certifications { get; set; } = [];

    /// <summary>
    /// Gets or sets whether Skills is on.
    /// </summary>
    [BindNever]
    public bool SkillsEnabled { get; set; }
}
