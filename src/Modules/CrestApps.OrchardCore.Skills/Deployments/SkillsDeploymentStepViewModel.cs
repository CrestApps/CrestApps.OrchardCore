namespace CrestApps.OrchardCore.Skills.Deployments;

/// <summary>
/// The editor of the Skills deployment step.
/// </summary>
public class SkillsDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets whether the skills people hold are exported too.
    /// </summary>
    public bool IncludeAssignments { get; set; }

    /// <summary>
    /// Gets or sets whether people's certificates are exported too.
    /// </summary>
    public bool IncludeCertificationRecords { get; set; }
}
