using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Drivers;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Speech;

public sealed class AIProfileSpeechTests
{
    [Fact]
    public void Pronunciations_RoundTrip_WithCaseMatching()
    {
        // Act
        var entries = AIProfileSpeechDisplayDriver.ParsePronunciations("CrestApps = Crest Apps\n!SQL = sequel\nno equals sign\n = nothing\nword =\n");

        // Assert
        Assert.Equal(2, entries.Count);
        Assert.Equal("CrestApps", entries[0].Text);
        Assert.Equal("Crest Apps", entries[0].Alias);
        Assert.False(entries[0].MatchCase);
        Assert.Equal("SQL", entries[1].Text);
        Assert.True(entries[1].MatchCase);
        Assert.Equal($"CrestApps = Crest Apps{Environment.NewLine}!SQL = sequel", AIProfileSpeechDisplayDriver.FormatPronunciations(entries));
    }
}
