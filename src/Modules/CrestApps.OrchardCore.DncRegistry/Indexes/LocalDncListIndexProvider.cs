using CrestApps.OrchardCore.DncRegistry.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.DncRegistry.Indexes;

/// <summary>
/// Index provider that maps <see cref="LocalDncList"/> documents to <see cref="LocalDncListIndex"/>.
/// </summary>
public sealed class LocalDncListIndexProvider : IndexProvider<LocalDncList>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDncListIndexProvider"/> class.
    /// </summary>
    public LocalDncListIndexProvider()
    {
        CollectionName = DncRegistryConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<LocalDncList> context)
    {
        context.For<LocalDncListIndex>()
            .Map(list => new LocalDncListIndex
            {
                ListId = list.ListId,
                CountryCode = list.CountryCode,
                Name = list.Name,
                Status = list.Status,
                CreatedUtc = list.CreatedUtc,
            });
    }
}
