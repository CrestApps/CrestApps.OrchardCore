using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
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
    private readonly AzureAISearchAIDataSourceHandler _handler;

    public AzureAISearchAIDataSourceHandlerTests()
    {
        var stringLocalizerMock = new Mock<IStringLocalizer<AzureAISearchAIDataSourceHandler>>();
        stringLocalizerMock
            .Setup(s => s[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _handler = new AzureAISearchAIDataSourceHandler(stringLocalizerMock.Object);
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

    [Theory]
    [InlineData("category eq 'documentation'")]
    [InlineData("status ne 'archived'")]
    [InlineData("priority gt 5")]
    [InlineData("rating ge 4.5")]
    [InlineData("age lt 30")]
    [InlineData("score le 100")]
    [InlineData("category eq 'docs' and status eq 'published'")]
    [InlineData("status eq 'active' or status eq 'pending'")]
    [InlineData("not (status eq 'deleted')")]
    [InlineData("category eq 'docs' and (status eq 'published' or status eq 'draft')")]
    [InlineData("search.in(category, 'category1,category2')")]
    [InlineData("geo.distance(location, geography'POINT(-122.131577 47.678581)') le 5")]
    public async Task ValidatedAsync_WhenFilterIsValidOData_ShouldSucceed(string filter)
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
            Filter = filter,
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.True(result.Succeeded, $"Filter '{filter}' should be valid");
    }

    [Theory]
    [InlineData("category = 'documentation'")]  // Using = instead of eq
    [InlineData("status eq 'unmatched")]  // Unbalanced quotes
    [InlineData("category eq 'docs' and (status eq 'published'")]  // Unbalanced parentheses
    [InlineData("(status eq 'active'")]  // Unbalanced parentheses
    public async Task ValidatedAsync_WhenFilterIsInvalidOData_ShouldFail(string filter)
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
            Filter = filter,
        });

        var result = new ValidationResultDetails();
        var context = new ValidatedContext<AIDataSource>(dataSource, result);

        // Act
        await _handler.ValidatedAsync(context);

        // Assert
        Assert.False(result.Succeeded, $"Filter '{filter}' should be invalid");
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
