using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources.FileSources;

/// <summary>
/// Checks which records the File Sources screens claim.
/// </summary>
/// <remarks>
/// A file source and a web crawler are the same stored record type, told apart only by their source. Get
/// this wrong in either direction and a file source lists among the crawlers, or its editor grows a second
/// name and a target Web data source it does not have.
/// </remarks>
public sealed class FileSourceRecordsTests
{
    private static readonly IReadOnlyList<IngestionConnectorDescriptor> _connectors =
    [
        Descriptor(LocalFolderIngestionConnector.ConnectorName),
        Descriptor("Ftp"),
        Descriptor("Sftp"),
    ];

    [Theory]
    [InlineData("LocalFolder")]
    [InlineData("Ftp")]
    [InlineData("Sftp")]
    [InlineData("localfolder")]
    [InlineData("SFTP")]
    public void ARegisteredConnector_IsAFileSource(string source)
        => Assert.True(FileSourceRecords.IsConnector(source, _connectors));

    [Theory]
    [InlineData("Sitemap")]
    [InlineData("Web")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnythingElse_IsNotAFileSource(string source)
        => Assert.False(FileSourceRecords.IsConnector(source, _connectors));

    [Fact]
    public void AnUnregisteredConnector_IsNotAFileSource()
    {
        // A source nothing registers belongs to neither screen: whatever registered it is gone, and the
        // record cannot be run either way. Showing it here would offer a Read button that does nothing.
        Assert.False(FileSourceRecords.IsConnector("Dropbox", _connectors));
    }

    [Fact]
    public void SelectFileSources_KeepsOnlyConnectorBackedRecords()
    {
        WebCrawler[] records =
        [
            new() { ItemId = "a", Source = "LocalFolder" },
            new() { ItemId = "b", Source = "Sitemap" },
            new() { ItemId = "c", Source = "Sftp" },
            new() { ItemId = "d", Source = "Dropbox" },
        ];

        Assert.Equal(["a", "c"], FileSourceRecords.SelectFileSources(records, _connectors).Select(r => r.ItemId));
    }

    private static IngestionConnectorDescriptor Descriptor(string name)
        => new()
        {
            Name = name,
            DisplayName = new LocalizedString(name, name),
            Description = new LocalizedString(name, name),
        };
}
