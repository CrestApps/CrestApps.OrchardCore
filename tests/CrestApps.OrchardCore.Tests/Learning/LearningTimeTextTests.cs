using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// The transcript said "Time spent 0:00" for anything under a minute; it now says how long, as people read it.
/// </summary>
public sealed class LearningTimeTextTests
{
    [Theory]
    [InlineData(0, "None recorded")]
    [InlineData(45, "Less than a minute")]
    [InlineData(60, "1 min")]
    [InlineData(754, "12 min")]
    [InlineData(3900, "1 h 05 min")]
    public void Format_SaysHowLong(long seconds, string expected)
    {
        // Arrange
        var text = new LearningTimeText(new EchoLocalizer());

        // Act
        var result = text.Format(seconds);

        // Assert
        Assert.Equal(expected, result.Value);
    }

    private sealed class EchoLocalizer : IStringLocalizer<LearningTimeText>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
