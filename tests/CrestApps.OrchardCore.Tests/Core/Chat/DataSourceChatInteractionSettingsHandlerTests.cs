using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Chat.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.Chat;

public sealed class DataSourceChatInteractionSettingsHandlerTests
{
    [Fact]
    public async Task UpdatingAsync_WithDataSource_PersistsDataSourceAndDefaultRagMetadata()
    {
        var dataSourceCatalog = new Mock<ICatalog<AIDataSource>>();
        dataSourceCatalog
            .Setup(catalog => catalog.FindByIdAsync("datasource-1"))
            .ReturnsAsync(new AIDataSource { ItemId = "datasource-1" });

        var serviceProvider = new ServiceCollection()
            .AddSingleton(dataSourceCatalog.Object)
            .BuildServiceProvider();

        var handler = new DataSourceChatInteractionSettingsHandler(
            serviceProvider,
            NullLogger<DataSourceChatInteractionSettingsHandler>.Instance);

        using var document = JsonDocument.Parse("""{"dataSourceId":"datasource-1"}""");
        var interaction = new ChatInteraction();

        await handler.UpdatingAsync(interaction, document.RootElement);

        Assert.True(interaction.TryGet<DataSourceMetadata>(out var dataSourceMetadata));
        Assert.Equal("datasource-1", dataSourceMetadata.DataSourceId);

        Assert.True(interaction.TryGet<AIDataSourceRagMetadata>(out var ragMetadata));
        Assert.False(ragMetadata.IsInScope);
        Assert.Null(ragMetadata.Strictness);
        Assert.Null(ragMetadata.TopNDocuments);
        Assert.Null(ragMetadata.Filter);
    }

    [Fact]
    public async Task UpdatingAsync_WithoutDataSource_ClearsDataSourceAndPreservesRetrievedDocuments()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var handler = new DataSourceChatInteractionSettingsHandler(
            serviceProvider,
            NullLogger<DataSourceChatInteractionSettingsHandler>.Instance);

        using var document = JsonDocument.Parse("""{"topNDocuments":8}""");
        var interaction = new ChatInteraction();
        interaction.Put(new DataSourceMetadata { DataSourceId = "datasource-1" });
        interaction.Put(new AIDataSourceRagMetadata { IsInScope = true, Strictness = 3, TopNDocuments = 5, Filter = "category eq 'docs'" });

        await handler.UpdatingAsync(interaction, document.RootElement);

        Assert.True(interaction.TryGet<DataSourceMetadata>(out var dataSourceMetadata));
        Assert.Null(dataSourceMetadata.DataSourceId);

        Assert.True(interaction.TryGet<AIDataSourceRagMetadata>(out var ragMetadata));
        Assert.Null(ragMetadata.Strictness);
        Assert.Equal(8, ragMetadata.TopNDocuments);
        Assert.Null(ragMetadata.Filter);
        Assert.False(ragMetadata.IsInScope);
    }
}
