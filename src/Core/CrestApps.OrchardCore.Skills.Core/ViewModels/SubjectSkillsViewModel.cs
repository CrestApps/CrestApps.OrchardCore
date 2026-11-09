namespace CrestApps.OrchardCore.Skills.Core.ViewModels;

/// <summary>
/// The Skills section an editor shows for whoever holds skills, such as a user or a worker: one proficiency choice per
/// enabled skill.
/// </summary>
public class SubjectSkillsViewModel
{
    /// <summary>
    /// Gets or sets one row per enabled skill.
    /// </summary>
    public IList<SubjectSkillRowViewModel> Skills { get; set; } = [];
}
