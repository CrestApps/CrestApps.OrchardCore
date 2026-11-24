using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Staff;

/// <summary>
/// The Contact Center card of the staff editor: whether the person is an agent, their presence, the queues they may
/// work and their skills, with a link to their entitlements.
/// </summary>
internal sealed class AgentStaffDisplayDriver : DisplayDriver<StaffMember>
{
    private readonly IAgentProfileManager _agentManager;

    public AgentStaffDisplayDriver(IAgentProfileManager agentManager)
    {
        _agentManager = agentManager;
    }

    public override async Task<IDisplayResult> EditAsync(StaffMember staff, BuildEditorContext context)
    {
        if (string.IsNullOrEmpty(staff.UserId))
        {
            return null;
        }

        var agent = await _agentManager.FindByUserIdAsync(staff.UserId);

        return Initialize<AgentStaffViewModel>("AgentStaff_Edit", model =>
        {
            model.AgentId = agent?.ItemId;
            model.Presence = agent?.PresenceStatus.ToString();
            model.AllowedQueueCount = agent?.AllowedQueueIds?.Count ?? 0;
            model.AllowedCampaignCount = agent?.AllowedCampaignIds?.Count ?? 0;
            model.Skills = agent?.SkillProficiencies?.Select(skill => $"{skill.SkillId} ({skill.Proficiency})").ToList() ?? [];
            model.MaxConcurrentInteractions = agent?.MaxConcurrentInteractions ?? 0;
        }).Location("Content:8");
    }
}

/// <summary>
/// The values of the Contact Center card of the staff editor.
/// </summary>
public class AgentStaffViewModel
{
    public string AgentId { get; set; }

    public string Presence { get; set; }

    public int AllowedQueueCount { get; set; }

    public int AllowedCampaignCount { get; set; }

    public IList<string> Skills { get; set; } = [];

    public int MaxConcurrentInteractions { get; set; }
}
