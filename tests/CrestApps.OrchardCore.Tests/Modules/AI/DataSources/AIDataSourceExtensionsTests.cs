using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Extensions;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// The data source helpers the AI.DataSources provider modules used to copy, now shared from AI.Core.
/// </summary>
public sealed class AIDataSourceExtensionsTests
{
    [Fact]
    public void GetSourceType_WhenNoSourceIsSet_ShouldReadASearchIndexProfile()
    {
        // Arrange
        var dataSource = new AIDataSource();

        // Act
        var sourceType = dataSource.GetSourceType();

        // Assert
        Assert.Equal(AIDataSourceSourceTypes.SearchIndexProfile, sourceType);
    }

    [Fact]
    public void GetSourceType_WhenASourceIsSet_ShouldReturnIt()
    {
        // Arrange
        var dataSource = new AIDataSource { Source = "AzureAISearch" };

        // Act
        var sourceType = dataSource.GetSourceType();

        // Assert
        Assert.Equal("AzureAISearch", sourceType);
    }

    [Theory]
    [InlineData("  Release notes  \r\nMore text", "Release notes")]
    [InlineData("Single line", "Single line")]
    [InlineData("\nStarts with a newline", "Starts with a newline")]
    public void GetTitleFromContent_ShouldReturnTheTrimmedFirstLine(string content, string expected)
    {
        // Act
        var title = AIDataSourceExtensions.GetTitleFromContent(content);

        // Assert
        Assert.Equal(expected, title);
    }

    [Fact]
    public void GetTitleFromContent_WhenTheFirstLineIsLong_ShouldCutIt()
    {
        // Arrange
        var content = new string('a', 500);

        // Act
        var title = AIDataSourceExtensions.GetTitleFromContent(content);

        // Assert
        Assert.Equal(AIDataSourceExtensions.MaxContentTitleLength, title.Length);
    }
}
