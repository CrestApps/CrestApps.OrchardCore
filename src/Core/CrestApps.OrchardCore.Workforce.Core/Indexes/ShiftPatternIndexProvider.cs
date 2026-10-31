using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps rotations to <see cref="ShiftPatternIndex"/> rows.
/// </summary>
public sealed class ShiftPatternIndexProvider : IndexProvider<ShiftPattern>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftPatternIndexProvider"/> class.
    /// </summary>
    public ShiftPatternIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ShiftPattern> context)
    {
        context.For<ShiftPatternIndex>()
            .Map(item => new ShiftPatternIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                TeamId = item.TeamId,
                Enabled = item.Enabled,
            });
    }
}
