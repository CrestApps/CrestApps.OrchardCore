using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps coverage requirements to <see cref="CoverageRequirementIndex"/> rows.
/// </summary>
public sealed class CoverageRequirementIndexProvider : IndexProvider<CoverageRequirement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageRequirementIndexProvider"/> class.
    /// </summary>
    public CoverageRequirementIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<CoverageRequirement> context)
    {
        context.For<CoverageRequirementIndex>()
            .Map(item => new CoverageRequirementIndex
            {
                ItemId = item.ItemId,
                TeamId = item.TeamId,
                SkillId = item.SkillId,
            });
    }
}
