using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// One skill a queue asks of the agent who takes its work, as edited on the queue screen.
/// </summary>
public class QueueSkillRequirementViewModel
{
    public string SkillId { get; set; }

    public int MinimumProficiency { get; set; } = AgentSkill.DefaultProficiency;

    public bool Required { get; set; } = true;

    public int RelaxAfterSeconds { get; set; }
}
