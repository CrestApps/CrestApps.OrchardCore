using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Core.Omnichannel.Services;

public sealed class OmnichannelSmsComplianceHelperTests
{
    [Theory]
    [InlineData("STOP")]
    [InlineData("stop")]
    [InlineData("Stop texting me")]
    [InlineData("UNSUBSCRIBE")]
    public void IsOptOutRequest_WhenMessageMatchesDefaultKeyword_ShouldReturnTrue(string message)
    {
        // Act
        var result = OmnichannelSmsComplianceHelper.IsOptOutRequest(message);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("stopwatch")]
    [InlineData("endoscopy")]
    public void IsOptOutRequest_WhenMessageDoesNotMatchKeywordBoundary_ShouldReturnFalse(string message)
    {
        // Act
        var result = OmnichannelSmsComplianceHelper.IsOptOutRequest(message);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ParseOptOutKeywords_WhenKeywordsAreDelimited_ShouldNormalizeDistinctValues()
    {
        // Act
        var keywords = OmnichannelSmsComplianceHelper.ParseOptOutKeywords("pause, STOP; pause");

        // Assert
        Assert.Equal(["pause", "STOP"], keywords);
    }
}
