using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An entry written before provider copies were tracked per provider recorded one provider and one reference. When
/// it is read, that pair has to become an entry in the map every reader now works from, or the copy would be
/// neither played, re-synced nor deleted.
/// </summary>
public sealed class VoiceMediaItemCompatibilityTests
{
    [Fact]
    public void FoldLegacyReference_AddsTheSingleCopyToTheMap()
    {
        // Arrange
        var item = new VoiceMediaItem
        {
            ProviderName = "Telnyx",
            MediaReference = "legacy-copy",
            ProviderReferences = null,
        };

        // Act
        VoiceMediaItemCompatibility.FoldLegacyReference(item);

        // Assert
        Assert.Equal("legacy-copy", Assert.Single(item.ProviderReferences).Value);
        Assert.Equal("Telnyx", Assert.Single(item.ProviderReferences).Key);
    }

    [Fact]
    public void FoldLegacyReference_LeavesAProviderAlreadyInTheMapAlone()
    {
        // Arrange
        // The map is the record; the old pair only mirrors it, and must not undo a copy the map knows is newer.
        var item = new VoiceMediaItem
        {
            ProviderName = "telnyx",
            MediaReference = "stale-copy",
            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "current-copy" },
        };

        // Act
        VoiceMediaItemCompatibility.FoldLegacyReference(item);

        // Assert
        Assert.Equal("current-copy", Assert.Single(item.ProviderReferences).Value);
    }

    [Fact]
    public void FoldLegacyReference_WithoutAPair_OnlyMakesSureTheMapExists()
    {
        // Arrange
        var item = new VoiceMediaItem { ProviderReferences = null };

        // Act
        VoiceMediaItemCompatibility.FoldLegacyReference(item);

        // Assert
        Assert.NotNull(item.ProviderReferences);
        Assert.Empty(item.ProviderReferences);
    }
}
