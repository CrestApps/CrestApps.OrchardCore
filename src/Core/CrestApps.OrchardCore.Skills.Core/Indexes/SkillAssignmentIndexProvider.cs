using CrestApps.OrchardCore.Skills.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// Maps skill assignments to <see cref="SkillAssignmentIndex"/> rows.
/// </summary>
public sealed class SkillAssignmentIndexProvider : IndexProvider<SkillAssignment>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkillAssignmentIndexProvider"/> class.
    /// </summary>
    public SkillAssignmentIndexProvider()
    {
        CollectionName = SkillsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SkillAssignment> context)
    {
        context.For<SkillAssignmentIndex>()
            .Map(assignment => new SkillAssignmentIndex
            {
                ItemId = assignment.ItemId,
                SubjectKind = assignment.SubjectKind,
                SubjectId = assignment.SubjectId,
                SkillId = assignment.SkillId,
                Proficiency = assignment.Proficiency,
                EffectiveFrom = SkillsDates.ToKey(assignment.EffectiveFrom),
                EffectiveTo = SkillsDates.ToKey(assignment.EffectiveTo),
            });
    }
}
