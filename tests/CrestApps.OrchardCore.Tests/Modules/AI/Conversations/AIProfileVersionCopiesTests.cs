using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIProfileVersionCopiesTests
{
    [Fact]
    public async Task GetOrCreateAsync_ReturnsTheProfileItselfForItsCurrentVersion()
    {
        // Arrange
        var (copies, _, source) = Create(currentVersion: 4);

        // Act
        var profile = await copies.GetOrCreateAsync(source.ItemId, 4, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(source, profile);
    }

    [Fact]
    public async Task GetOrCreateAsync_CopiesAnEarlierVersionOnce_MarkedWithItsSource()
    {
        // Arrange
        var (copies, created, source) = Create(currentVersion: 4);

        // Act
        var first = await copies.GetOrCreateAsync(source.ItemId, 3, TestContext.Current.CancellationToken);
        var second = await copies.GetOrCreateAsync(source.ItemId, 3, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(created);
        Assert.Same(first, second);
        Assert.Equal("support-v3", first.Name);
        Assert.Equal("Old instructions", first.WelcomeMessage);
        Assert.True(first.TryGet<AIProfileVersionCopy>(out var marker));
        Assert.Equal(source.ItemId, marker.SourceProfileId);
        Assert.Equal(3, marker.Version);
        Assert.Contains("version 3", first.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsNothingForAVersionThatDoesNotExist()
    {
        // Arrange
        var (copies, created, source) = Create(currentVersion: 4);

        // Act
        var profile = await copies.GetOrCreateAsync(source.ItemId, 9, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(profile);
        Assert.Empty(created);
    }

    private static (AIProfileVersionCopies Copies, List<AIProfile> Created, AIProfile Source) Create(int currentVersion)
    {
        var source = new AIProfile
        {
            ItemId = "profile-1",
            Name = "support",
            DisplayText = "Support",
            WelcomeMessage = "New instructions",
        };
        source.Put(new AIProfileVersionMetadata { Version = currentVersion });

        var oldSnapshot = new AIProfile
        {
            ItemId = "profile-1",
            Name = "support",
            DisplayText = "Support",
            WelcomeMessage = "Old instructions",
        };
        oldSnapshot.Put(new AIProfileVersionMetadata { Version = 3 });

        var created = new List<AIProfile>();
        var profileManager = new Mock<IAIProfileManager>();
        profileManager.Setup(x => x.FindByIdAsync("profile-1", It.IsAny<CancellationToken>())).ReturnsAsync(source);
        profileManager.Setup(x => x.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => created.FirstOrDefault(profile => profile.Name == name));
        profileManager.Setup(x => x.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, JsonNode _, CancellationToken _) => new AIProfile { ItemId = "copy-" + name, Name = name });
        profileManager.Setup(x => x.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .Callback<AIProfile, CancellationToken>((profile, _) => created.Add(profile))
            .Returns(ValueTask.CompletedTask);

        var versionStore = new Mock<IAIProfileVersionStore>();
        versionStore.Setup(x => x.PageAsync("profile-1", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([new AIProfileVersion { ProfileId = "profile-1", Version = 3, Snapshot = oldSnapshot }], 1));

        return (new AIProfileVersionCopies(profileManager.Object, versionStore.Object, NullLogger<AIProfileVersionCopies>.Instance), created, source);
    }
}
