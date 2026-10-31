namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// The Skills section of the user editor.
/// </summary>
public class UserSkillsViewModel
{
    /// <summary>
    /// Gets or sets one row per enabled skill.
    /// </summary>
    public IList<UserSkillRowViewModel> Skills { get; set; } = [];
}
