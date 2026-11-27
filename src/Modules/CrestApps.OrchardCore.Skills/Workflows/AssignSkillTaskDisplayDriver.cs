using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// The editor of the Assign Skill workflow task.
/// </summary>
public sealed class AssignSkillTaskDisplayDriver : ActivityDisplayDriver<AssignSkillTask, AssignSkillTaskViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssignSkillTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssignSkillTaskDisplayDriver(IStringLocalizer<AssignSkillTaskDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AssignSkillTask activity, BuildEditorContext context)
        => Initialize<AssignSkillTaskViewModel>("AssignSkillTask_Fields_Edit", model => EditActivity(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Skill"], "10"));

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
