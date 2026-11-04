using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps LaborRuleOverride records to <see cref="LaborRuleOverrideIndex"/> rows.
/// </summary>
public sealed class LaborRuleOverrideIndexProvider : IndexProvider<LaborRuleOverride>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LaborRuleOverrideIndexProvider"/> class.
    /// </summary>
    public LaborRuleOverrideIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<LaborRuleOverride> context)
    {
        context.For<LaborRuleOverrideIndex>()
            .Map(item => new LaborRuleOverrideIndex
            {
                ItemId = item.ItemId,
                TeamId = item.TeamId,
                WorkerId = item.WorkerId,
                CreatedUtc = item.CreatedUtc,
            });
    }
}
