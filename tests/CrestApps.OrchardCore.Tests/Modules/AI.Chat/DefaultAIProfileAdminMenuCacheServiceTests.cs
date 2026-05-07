using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

using AIChatProfileSettings = CrestApps.OrchardCore.AI.Chat.Models.AIChatProfileSettings;

public sealed class DefaultAIProfileAdminMenuCacheServiceTests
{
    [Fact]
    public async Task GetProfilesAsync_CachesFilteredProfilesUntilInvalidated()
    {
        var store = new Mock<IAIProfileStore>();
        store.SetupSequence(x => x.GetByTypeAsync(AIProfileType.Chat, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyCollection<AIProfile>>(
            [
                CreateProfile("chat-a", true),
                CreateProfile("chat-b", false),
            ]))
            .Returns(new ValueTask<IReadOnlyCollection<AIProfile>>(
            [
                CreateProfile("chat-c", true),
            ]));

        var services = new ServiceCollection()
            .AddScoped<IAIProfileStore>(_ => store.Object)
            .AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions()))
            .AddSingleton<IAIProfileAdminMenuCacheService, DefaultAIProfileAdminMenuCacheService>()
            .BuildServiceProvider();

        var cacheService = services.GetRequiredService<IAIProfileAdminMenuCacheService>();

        var firstProfiles = await cacheService.GetProfilesAsync(TestContext.Current.CancellationToken);
        var secondProfiles = await cacheService.GetProfilesAsync(TestContext.Current.CancellationToken);

        await cacheService.InvalidateAsync(TestContext.Current.CancellationToken);

        var refreshedProfiles = await cacheService.GetProfilesAsync(TestContext.Current.CancellationToken);

        Assert.Single(firstProfiles);
        Assert.Equal("chat-a", firstProfiles[0].Name);
        Assert.Single(secondProfiles);
        Assert.Equal("chat-a", secondProfiles[0].Name);
        Assert.Single(refreshedProfiles);
        Assert.Equal("chat-c", refreshedProfiles[0].Name);
        store.Verify(x => x.GetByTypeAsync(AIProfileType.Chat, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetProfilesAsync_ReturnsClonedProfiles()
    {
        var store = new Mock<IAIProfileStore>();
        store.Setup(x => x.GetByTypeAsync(AIProfileType.Chat, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyCollection<AIProfile>>([CreateProfile("chat-a", true)]));

        var services = new ServiceCollection()
            .AddScoped<IAIProfileStore>(_ => store.Object)
            .AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions()))
            .AddSingleton<IAIProfileAdminMenuCacheService, DefaultAIProfileAdminMenuCacheService>()
            .BuildServiceProvider();

        var cacheService = services.GetRequiredService<IAIProfileAdminMenuCacheService>();

        var firstProfiles = await cacheService.GetProfilesAsync(TestContext.Current.CancellationToken);
        firstProfiles[0].DisplayText = "modified";

        var secondProfiles = await cacheService.GetProfilesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("chat-a", secondProfiles[0].DisplayText);
    }

    private static AIProfile CreateProfile(string name, bool isOnAdminMenu)
    {
        var profile = new AIProfile
        {
            ItemId = name,
            Name = name,
            DisplayText = name,
            Type = AIProfileType.Chat,
        };

        profile.AlterSettings<AIChatProfileSettings>(settings =>
        {
            settings.IsOnAdminMenu = isOnAdminMenu;
        });

        return profile;
    }
}
