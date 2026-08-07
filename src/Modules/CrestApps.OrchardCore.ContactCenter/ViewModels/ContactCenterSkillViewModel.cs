using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for a Contact Center skill.
/// </summary>
public class ContactCenterSkillViewModel
{
    /// <summary>
    /// Gets or sets the skill identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique skill name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the skill description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skill is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
