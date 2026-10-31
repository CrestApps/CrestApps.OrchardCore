using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps shift templates to <see cref="ShiftTemplateIndex"/> rows.
/// </summary>
public sealed class ShiftTemplateIndexProvider : IndexProvider<ShiftTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftTemplateIndexProvider"/> class.
    /// </summary>
    public ShiftTemplateIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ShiftTemplate> context)
    {
        context.For<ShiftTemplateIndex>()
            .Map(item => new ShiftTemplateIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Enabled = item.Enabled,
            });
    }
}
