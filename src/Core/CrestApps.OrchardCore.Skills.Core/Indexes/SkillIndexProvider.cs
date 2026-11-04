using CrestApps.OrchardCore.Skills.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// Maps skills to <see cref="SkillIndex"/> rows.
/// </summary>
public sealed class SkillIndexProvider : IndexProvider<Skill>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkillIndexProvider"/> class.
    /// </summary>
    public SkillIndexProvider()
    {
        CollectionName = SkillsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Skill> context)
    {
        context.For<SkillIndex>()
            .Map(skill => new SkillIndex
            {
                ItemId = skill.ItemId,
                Name = skill.Name,
                NormalizedName = SkillsNames.Normalize(skill.Name),
                Category = skill.Category,
                Enabled = skill.Enabled,
            });
    }
}
