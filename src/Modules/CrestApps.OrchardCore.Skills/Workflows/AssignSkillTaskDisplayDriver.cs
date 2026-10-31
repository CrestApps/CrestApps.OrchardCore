using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// The editor of the Assign Skill workflow task.
/// </summary>
public sealed class AssignSkillTaskDisplayDriver : ActivityDisplayDriver<AssignSkillTask, AssignSkillTaskViewModel>
{
    /// <inheritdoc />
    protected override void EditActivity(AssignSkillTask activity, AssignSkillTaskViewModel model)
    {
        model.SubjectKind = activity.SubjectKind;
        model.SubjectId = activity.SubjectId;
        model.Skill = activity.Skill;
        model.Proficiency = activity.Proficiency;
    }

    /// <inheritdoc />
    protected override void UpdateActivity(AssignSkillTaskViewModel model, AssignSkillTask activity)
    {
        activity.SubjectKind = model.SubjectKind?.Trim();
        activity.SubjectId = model.SubjectId?.Trim();
        activity.Skill = model.Skill?.Trim();
        activity.Proficiency = model.Proficiency?.Trim();
    }
}
