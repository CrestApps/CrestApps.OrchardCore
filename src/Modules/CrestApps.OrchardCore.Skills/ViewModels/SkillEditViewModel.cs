using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// The skill editor.
/// </summary>
public class SkillEditViewModel
{
    /// <summary>
    /// Gets or sets the skill id, or empty for a new skill.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the certification the skill requires.
    /// </summary>
    public string RequiredCertificationId { get; set; }

    /// <summary>
    /// Gets or sets whether the skill is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the certifications to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CertificationOptions { get; set; } = [];
}
