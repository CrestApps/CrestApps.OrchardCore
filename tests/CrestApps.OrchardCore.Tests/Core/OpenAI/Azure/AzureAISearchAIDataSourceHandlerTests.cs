using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Core.OpenAI.Azure;

public sealed class AzureAISearchAIDataSourceHandlerTests
{
    private readonly AzureAIDataSourceHandler _handler;

    public AzureAISearchAIDataSourceHandlerTests()
    {
        var stringLocalizerMock = new Mock<IStringLocalizer<AzureAIDataSourceHandler>>();
        stringLocalizerMock
            .Setup(s => s[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _handler = new AzureAIDataSourceHandler(stringLocalizerMock.Object);
    }

    [Fact]
    public async Task ValidatedAsync_WhenIndexNameIsEmpty_ShouldFail()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ProfileSource = AzureOpenAIConstants.ProviderName,
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIDataSourceIndexMetadata
        {
            IndexName = "",
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ValidatedAsync_WhenIndexNameIsValid_ShouldSucceed()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ProfileSource = AzureOpenAIConstants.ProviderName,
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIDataSourceIndexMetadata
        {
            IndexName = "my-index",
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidatedAsync_WhenProfileSourceIsNotAzureOpenAI_ShouldSkipValidation()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ProfileSource = "OtherSource",
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIDataSourceIndexMetadata
        {
            IndexName = "",
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidatedAsync_WhenTypeIsNotAzureAISearch_ShouldSkipValidation()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ProfileSource = AzureOpenAIConstants.ProviderName,
            Type = "OtherType",
        };

        dataSource.Put(new AzureAIDataSourceIndexMetadata
        {
            IndexName = "",
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.True(result.Succeeded);
    }
}
