using CrestApps.Core.AI.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class AIVisitorIdentityOptionsRegistrationTests
{
    [Fact]
    public void ConfigureServices_AppliesVisitorIdentitySiteSettings()
    {
        var settings = new AIVisitorIdentityOptions
        {
            CookieName = "chat-visitor",
            CookieLifetime = TimeSpan.FromDays(30),
            RemoteAddressMode = AIVisitorRemoteAddressMode.Encrypted,
            RemoteAddressHashSalt = "tenant-salt",
        };

        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(settings));

        new CrestApps.OrchardCore.AI.Chat.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AIVisitorIdentityOptions>>().Value;

        Assert.Equal("chat-visitor", options.CookieName);
        Assert.Equal(TimeSpan.FromDays(30), options.CookieLifetime);
        Assert.Equal(AIVisitorRemoteAddressMode.Encrypted, options.RemoteAddressMode);
        Assert.Equal("tenant-salt", options.RemoteAddressHashSalt);
    }

    private static ISiteService CreateSiteService(AIVisitorIdentityOptions settings)
    {
        var site = new Mock<ISite>();
        site.Setup(x => x.GetOrCreate<AIVisitorIdentityOptions>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
