using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The hold music picker on the queue editor's "While callers wait" card lists the voice media recordings by name.
/// </summary>
public sealed class HoldMusicOptionsTests
{
    [Fact]
    public async Task GetVoiceMediaOptionsAsync_ListsTheRecordingsByName_WithTheSelectedOneSelected()
    {
        // Arrange
        var provider = AdminFormOptionsProviderFactory.Create(voiceMedia:
        [
            new VoiceMediaItem { ItemId = "media-2", Name = "Waltz" },
            new VoiceMediaItem { ItemId = "media-1", Name = "Jazz" },
        ]);

        // Act
        var options = await provider.GetVoiceMediaOptionsAsync("media-2");

        // Assert
        Assert.Equal(["Jazz", "Waltz"], options.Select(option => option.Text));
        Assert.Equal(["media-2"], options.Where(option => option.Selected).Select(option => option.Value));
    }

    // A recording deleted from the catalog is still what the queue plays until someone picks another. Left out of the
    // list, the browser would post the first recording instead, and saving the queue for any other reason would quietly
    // change its hold music.
    [Fact]
    public async Task GetVoiceMediaOptionsAsync_KeepsASelectedRecordingTheCatalogNoLongerHas()
    {
        // Arrange
        var provider = AdminFormOptionsProviderFactory.Create(voiceMedia:
        [
            new VoiceMediaItem { ItemId = "media-1", Name = "Jazz" },
        ]);

        // Act
        var options = await provider.GetVoiceMediaOptionsAsync(" media-gone ");

        // Assert
        Assert.Equal(2, options.Count);
        var kept = Assert.Single(options, option => option.Value == "media-gone");
        Assert.True(kept.Selected);
        Assert.Equal("media-gone", kept.Text);
        Assert.False(Assert.Single(options, option => option.Value == "media-1").Selected);
    }

    [Fact]
    public async Task GetVoiceMediaOptionsAsync_WithNothingSelected_AddsNothing()
    {
        // Arrange
        var provider = AdminFormOptionsProviderFactory.Create(voiceMedia:
        [
            new VoiceMediaItem { ItemId = "media-1", Name = "Jazz" },
        ]);

        // Act
        var options = await provider.GetVoiceMediaOptionsAsync(selectedMediaId: null);

        // Assert
        Assert.Equal(["media-1"], options.Select(option => option.Value));
        Assert.DoesNotContain(options, option => option.Selected);
    }
}
