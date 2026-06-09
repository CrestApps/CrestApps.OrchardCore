using System.Text.Json;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry;

public sealed class LocalDncEntryIndexProviderTests
{
    [Fact]
    public void MapIndexes_WhenEntryContainsBatchRecords_ShouldReturnOneIndexPerRecord()
    {
        var entry = new LocalDncEntry
        {
            EntryId = "batch-entry-id-000000000000",
            ListId = "list-entry-id-0000000000000",
            CountryCode = "US",
            Records =
            [
                new LocalDncEntryRecord
                {
                    EntryId = "record-entry-id-00000000001",
                    PhoneNumber = "+12065550101",
                },
                new LocalDncEntryRecord
                {
                    EntryId = "record-entry-id-00000000002",
                    PhoneNumber = "+12065550102",
                },
            ],
        };

        var indexes = LocalDncEntryIndexProvider.MapIndexes(entry).ToList();

        Assert.Collection(indexes,
            first =>
            {
                Assert.Equal("record-entry-id-00000000001", first.EntryId);
                Assert.Equal("list-entry-id-0000000000000", first.ListId);
                Assert.Equal("US", first.CountryCode);
                Assert.Equal("+12065550101", first.PhoneNumber);
            },
            second =>
            {
                Assert.Equal("record-entry-id-00000000002", second.EntryId);
                Assert.Equal("list-entry-id-0000000000000", second.ListId);
                Assert.Equal("US", second.CountryCode);
                Assert.Equal("+12065550102", second.PhoneNumber);
            });
    }

    [Fact]
    public void MapIndexes_WhenEntryUsesLegacyPhoneNumber_ShouldReturnSingleIndex()
    {
        var entry = new LocalDncEntry
        {
            EntryId = "legacy-entry-id-000000000000",
            ListId = "list-entry-id-0000000000000",
            CountryCode = "CA",
            PhoneNumber = "+14165550123",
        };

        var indexes = LocalDncEntryIndexProvider.MapIndexes(entry).ToList();

        var index = Assert.Single(indexes);
        Assert.Equal("legacy-entry-id-000000000000", index.EntryId);
        Assert.Equal("list-entry-id-0000000000000", index.ListId);
        Assert.Equal("CA", index.CountryCode);
        Assert.Equal("+14165550123", index.PhoneNumber);
    }

    [Fact]
    public void Serialize_WhenEntryContainsFiveThousandRecords_ShouldStayWellBelowCommonTextLimits()
    {
        var entry = new LocalDncEntry
        {
            EntryId = new string('9', 26),
            ListId = new string('8', 26),
            CountryCode = "US",
            Records = Enumerable.Range(0, 5000)
                .Select(index => new LocalDncEntryRecord
                {
                    EntryId = index.ToString("D26"),
                    PhoneNumber = $"+1{index:D14}",
                })
                .ToList(),
        };

        var json = JsonSerializer.Serialize(entry);

        Assert.True(
            json.Length < 512_000,
            $"Expected the serialized LocalDncEntry batch to stay under 512 KB, but it was {json.Length} characters.");
    }
}
