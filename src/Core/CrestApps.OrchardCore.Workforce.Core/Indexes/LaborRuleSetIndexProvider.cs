using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps LaborRuleSet records to <see cref="LaborRuleSetIndex"/> rows.
/// </summary>
public sealed class LaborRuleSetIndexProvider : IndexProvider<LaborRuleSet>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LaborRuleSetIndexProvider"/> class.
    /// </summary>
    public LaborRuleSetIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<LaborRuleSet> context)
    {
        context.For<LaborRuleSetIndex>()
            .Map(item => new LaborRuleSetIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Enabled = item.Enabled,
            });
    }
}
