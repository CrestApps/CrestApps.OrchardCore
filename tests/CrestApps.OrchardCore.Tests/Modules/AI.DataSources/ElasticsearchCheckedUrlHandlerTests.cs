using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Handlers;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.DataConnectors.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// Review 3 (High 2): a self-managed Elasticsearch cluster connected with a private network allowed whatever its URL,
/// so only the save checked a URL. A save now records the URL it checked, and the client holds a recorded URL to the
/// same rule when it connects. Only a data source saved before the check, and not moved since, has no record: it keeps
/// its private cluster (review 8, G7). A save can't drop the record by leaving it out.
/// </summary>
public sealed class ElasticsearchCheckedUrlHandlerTests
{
    private const string Url = "https://search.example.com:9243";

    [Fact]
    public void Record_ANewDataSource_RecordsItsUrlAsChecked()
    {
        // Arrange
        var dataSource = SelfManaged(Url);

        // Act
        ElasticsearchCheckedUrlHandler.Record(dataSource, stored: null);

        // Assert
        Assert.True(dataSource.TryGet<ElasticsearchCheckedUrl>(out var record));
        Assert.Equal(Url, record.Url);
    }

    [Fact]
    public void Record_AUrlKeptFromADataSourceSavedBeforeTheCheck_StaysUnrecorded()
    {
        // Arrange
        var stored = SelfManaged("http://elasticsearch:9200");
        var dataSource = SelfManaged("http://elasticsearch:9200");

        // Act
        ElasticsearchCheckedUrlHandler.Record(dataSource, stored);

        // Assert
        Assert.False(dataSource.Has<ElasticsearchCheckedUrl>());
    }

    [Fact]
    public void Record_AUrlKeptFromACheckedDataSource_StaysRecorded_EvenWhenTheSaveLeavesTheRecordOut()
    {
        // Arrange
        var stored = SelfManaged(Url);
        stored.Put(new ElasticsearchCheckedUrl { Url = Url });
        var dataSource = SelfManaged(Url);

        // Act
        ElasticsearchCheckedUrlHandler.Record(dataSource, stored);

        // Assert
        Assert.True(dataSource.Has<ElasticsearchCheckedUrl>());
    }

    [Fact]
    public void Record_AChangedUrl_IsRecorded_EvenForADataSourceSavedBeforeTheCheck()
    {
        // Arrange
        var stored = SelfManaged("http://elasticsearch:9200");
        var dataSource = SelfManaged(Url);

        // Act
        ElasticsearchCheckedUrlHandler.Record(dataSource, stored);

        // Assert
        Assert.True(dataSource.Has<ElasticsearchCheckedUrl>());
    }

    [Fact]
    public void Record_ADataSourceThatReadsThroughADataConnection_HasNoRecord()
    {
        // Arrange
        var dataSource = SelfManaged(Url);
        dataSource.Put(new ElasticsearchCheckedUrl { Url = Url });
        dataSource.Put(new DataConnectionReference { ConnectionId = "4qkc7h2m9x1r5t8v3w6y0z2b4d" });

        // Act
        ElasticsearchCheckedUrlHandler.Record(dataSource, stored: null);

        // Assert
        Assert.False(dataSource.Has<ElasticsearchCheckedUrl>());
    }

    private static AIDataSource SelfManaged(string url)
    {
        var dataSource = new AIDataSource { ItemId = "ds-1", Source = AIDataSourceSourceTypes.Elasticsearch, DisplayText = "Logs" };
        dataSource.Put(new ElasticsearchSourceMetadata
        {
            EnvironmentType = ElasticsearchSourceMetadata.SelfManagedEnvironmentType,
            Url = url,
            AuthenticationType = ElasticsearchSourceMetadata.NoneAuthenticationType,
            IndexName = "logs",
        });

        return dataSource;
    }
}
