using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Core.OpenAI.Azure;

public sealed class AzureAISearchAIDataSourceHandlerTests
{
    private readonly AzureAISearchAIDataSourceHandler _handler;
    private readonly Mock<IODataFilterValidator> _validatorMock;

    public AzureAISearchAIDataSourceHandlerTests()
    {
        _validatorMock = new Mock<IODataFilterValidator>();
        
        var stringLocalizerMock = new Mock<IStringLocalizer<AzureAISearchAIDataSourceHandler>>();
        stringLocalizerMock
            .Setup(s => s[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _handler = new AzureAISearchAIDataSourceHandler(_validatorMock.Object, stringLocalizerMock.Object);
    }

    [Fact]
    public async Task ValidatedAsync_WhenIndexNameIsEmpty_ShouldFail()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ProfileSource = AzureOpenAIConstants.AzureOpenAIOwnData,
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIProfileAISearchMetadata
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
            ProfileSource = AzureOpenAIConstants.AzureOpenAIOwnData,
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIProfileAISearchMetadata
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
    public async Task ValidatedAsync_WhenFilterIsValid_ShouldSucceed()
    {
        // Arrange
        var filter = "category eq 'documentation'";
        _validatorMock.Setup(v => v.IsValid(filter)).Returns(true);

        var dataSource = new AIDataSource
        {
            ProfileSource = AzureOpenAIConstants.AzureOpenAIOwnData,
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIProfileAISearchMetadata
        {
            IndexName = "my-index",
            Filter = filter,
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.True(result.Succeeded);
        _validatorMock.Verify(v => v.IsValid(filter), Times.Once);
    }

    [Fact]
    public async Task ValidatedAsync_WhenFilterIsInvalid_ShouldFail()
    {
        // Arrange
        var filter = "invalid filter";
        _validatorMock.Setup(v => v.IsValid(filter)).Returns(false);

        var dataSource = new AIDataSource
        {
            ProfileSource = AzureOpenAIConstants.AzureOpenAIOwnData,
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIProfileAISearchMetadata
        {
            IndexName = "my-index",
            Filter = filter,
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.False(result.Succeeded);
        _validatorMock.Verify(v => v.IsValid(filter), Times.Once);
    }

    [Fact]
    public async Task ValidatedAsync_WhenFilterIsNull_ShouldSucceed()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ProfileSource = AzureOpenAIConstants.AzureOpenAIOwnData,
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIProfileAISearchMetadata
        {
            IndexName = "my-index",
            Filter = null,
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.True(result.Succeeded);
        _validatorMock.Verify(v => v.IsValid(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValidatedAsync_WhenFilterIsEmpty_ShouldSucceed()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ProfileSource = AzureOpenAIConstants.AzureOpenAIOwnData,
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIProfileAISearchMetadata
        {
            IndexName = "my-index",
            Filter = "",
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.True(result.Succeeded);
        _validatorMock.Verify(v => v.IsValid(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValidatedAsync_WhenProfileSourceIsNotAzureOpenAIOwnData_ShouldSkipValidation()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ProfileSource = "OtherSource",
            Type = AzureOpenAIConstants.DataSourceTypes.AzureAISearch,
        };

        dataSource.Put(new AzureAIProfileAISearchMetadata
        {
            IndexName = "",  // Invalid, but should be skipped
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
            ProfileSource = AzureOpenAIConstants.AzureOpenAIOwnData,
            Type = "OtherType",
        };

        dataSource.Put(new AzureAIProfileAISearchMetadata
        {
            IndexName = "",  // Invalid, but should be skipped
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.True(result.Succeeded);
    }
}
