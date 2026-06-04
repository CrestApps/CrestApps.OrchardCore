using CrestApps.OrchardCore.DncRegistry.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.DncRegistry.Indexes;

/// <summary>
/// Index provider that maps <see cref="LocalDncEntry"/> documents to <see cref="LocalDncEntryIndex"/>.
/// </summary>
public sealed class LocalDncEntryIndexProvider : IndexProvider<LocalDncEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDncEntryIndexProvider"/> class.
    /// </summary>
    public LocalDncEntryIndexProvider()
    {
        CollectionName = DncRegistryConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<LocalDncEntry> context)
    {
        context.For<LocalDncEntryIndex>()
            .Map(entry => new LocalDncEntryIndex
            {
                EntryId = entry.EntryId,
                ListId = entry.ListId,
                CountryCode = entry.CountryCode,
                PhoneNumber = entry.PhoneNumber,
            });
    }
}
