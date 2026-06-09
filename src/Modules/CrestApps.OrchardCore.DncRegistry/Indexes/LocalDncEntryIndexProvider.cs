using CrestApps.OrchardCore.DncRegistry.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.DncRegistry.Indexes;

/// <summary>
/// Index provider that maps <see cref="LocalDncEntry"/> documents to <see cref="LocalDncEntryIndex"/>.
/// Phone numbers are stored in E.164 format and indexed directly, with one index row per phone number.
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
            .Map(MapIndexes);
    }

    internal static IEnumerable<LocalDncEntryIndex> MapIndexes(LocalDncEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Records is { Count: > 0 })
        {
            return entry.Records
                .Where(record => !string.IsNullOrWhiteSpace(record.PhoneNumber))
                .Select(record => new LocalDncEntryIndex
                {
                    EntryId = record.EntryId,
                    ListId = entry.ListId,
                    CountryCode = entry.CountryCode,
                    PhoneNumber = record.PhoneNumber,
                });
        }

        if (string.IsNullOrWhiteSpace(entry.PhoneNumber))
        {
            return [];
        }

        return
        [
            new LocalDncEntryIndex
            {
                EntryId = entry.EntryId,
                ListId = entry.ListId,
                CountryCode = entry.CountryCode,
                PhoneNumber = entry.PhoneNumber,
            }
        ];
    }
}
