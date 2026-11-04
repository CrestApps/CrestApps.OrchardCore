using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps activity codes to <see cref="ActivityCodeIndex"/> rows.
/// </summary>
public sealed class ActivityCodeIndexProvider : IndexProvider<ActivityCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityCodeIndexProvider"/> class.
    /// </summary>
    public ActivityCodeIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ActivityCode> context)
    {
        context.For<ActivityCodeIndex>()
            .Map(code => new ActivityCodeIndex
            {
                ItemId = code.ItemId,
                Name = code.Name,
                Kind = code.Kind,
                Enabled = code.Enabled,
            });
    }
}
