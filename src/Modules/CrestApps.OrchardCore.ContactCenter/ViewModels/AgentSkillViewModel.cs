using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// One skill an agent holds and how well, as edited on the entitlement screen.
/// </summary>
public class AgentSkillViewModel
{
    public string SkillId { get; set; }

    public int Proficiency { get; set; } = AgentSkill.DefaultProficiency;
}
