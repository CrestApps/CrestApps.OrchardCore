using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A voice media entry travels in a deployment plan or a recipe as its JSON. The master copy reference and the
/// per-provider copies have to make the round trip, and a plan written before they existed still has to import.
/// </summary>
public sealed class VoiceMediaItemDeploymentRoundTripTests
{
    [Fact]
    public void Export_ThenPopulate_CarriesTheMasterCopyAndEveryProviderCopy()
    {
        // Arrange
        var source = new VoiceMediaItem
        {
            ItemId = "media-1",
            Name = "Hold music",
            Description = "Main line",
            Format = "mp3",
            ContentType = "audio/mpeg",
            MasterStorageReference = "voice-media-abc",
            ProviderName = "Telnyx",
            MediaReference = "telnyx-copy",
            ProviderReferences = new Dictionary<string, string>
            {
                ["Telnyx"] = "telnyx-copy",
                ["OtherProvider"] = "other-copy",
            },
        };

        // Act
        var json = ContactCenterDeploymentSerializer.Export(source);
        var imported = new VoiceMediaItem();
        ContactCenterDeploymentSerializer.Populate(imported, json);

        // Assert
        Assert.Equal("Hold music", imported.Name);
        Assert.Equal("audio/mpeg", imported.ContentType);
        Assert.Equal("voice-media-abc", imported.MasterStorageReference);
        Assert.Equal("Telnyx", imported.ProviderName);
        Assert.Equal("telnyx-copy", imported.MediaReference);
        Assert.Equal(2, imported.ProviderReferences.Count);
        Assert.Equal("telnyx-copy", imported.ProviderReferences["Telnyx"]);
        Assert.Equal("other-copy", imported.ProviderReferences["OtherProvider"]);
    }

    [Fact]
    public void Populate_FromAPlanWrittenBeforeMasterCopiesWereKept_StillImports_AndTheSingleCopyFoldsIntoTheMap()
    {
        // Arrange
        var json = new JsonObject
        {
            ["ItemId"] = "media-1",
            ["Name"] = "Support hold music",
            ["ProviderName"] = "Telnyx",
            ["MediaReference"] = "support-hold-music",
            ["Format"] = "mp3",
        };

        // Act
        var imported = new VoiceMediaItem();
        ContactCenterDeploymentSerializer.Populate(imported, json);
        VoiceMediaItemCompatibility.FoldLegacyReference(imported);

        // Assert
        Assert.Equal("Support hold music", imported.Name);
        Assert.Null(imported.MasterStorageReference);
        Assert.Null(imported.ContentType);
        Assert.Equal("support-hold-music", Assert.Single(imported.ProviderReferences).Value);
    }
}
