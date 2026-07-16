using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Security;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class AIProfilePromptSecuritySettingsTests
{
    [Fact]
    public void Profile_OverridesWrittenAsSettings_AreReadableByRateLimiter()
    {
        // Arrange: write throttle overrides the same way the profile driver does.
        var profile = new AIProfile();

        // Act
        profile.AlterSettings<PromptSecurityProfileSettings>(settings =>
        {
            settings.MaxMessagesPerWindow = 4;
            settings.RateLimitWindow = TimeSpan.FromSeconds(30);
            settings.MaxAnonymousSessionsPerWindow = 1;
            settings.AnonymousSessionRateLimitWindow = TimeSpan.FromSeconds(600);
        });

        // Assert: read the way DefaultChatRateLimiter / DefaultChatSessionStartRateLimiter do.
        Assert.True(profile.TryGetSettings<PromptSecurityProfileSettings>(out var stored));
        Assert.Equal(4, stored.MaxMessagesPerWindow);
        Assert.Equal(TimeSpan.FromSeconds(30), stored.RateLimitWindow);
        Assert.Equal(1, stored.MaxAnonymousSessionsPerWindow);
        Assert.Equal(TimeSpan.FromSeconds(600), stored.AnonymousSessionRateLimitWindow);
    }

    [Fact]
    public void Profile_WithoutOverrides_HasNoStoredThrottleSettings()
    {
        var profile = new AIProfile();

        Assert.False(profile.TryGetSettings<PromptSecurityProfileSettings>(out _));
    }

    [Fact]
    public void Template_OverridesStoredInProperties_RoundTrip()
    {
        // Arrange: write the same way the template driver does.
        var template = new AIProfileTemplate
        {
            Source = AITemplateSources.Profile,
        };

        // Act
        var settings = template.GetOrCreate<PromptSecurityProfileSettings>();
        settings.MaxMessagesPerWindow = 6;
        settings.RateLimitWindow = TimeSpan.FromSeconds(75);
        template.Put(settings);

        // Assert
        Assert.True(template.Properties.ContainsKey(nameof(PromptSecurityProfileSettings)));

        var stored = template.GetOrCreate<PromptSecurityProfileSettings>();
        Assert.Equal(6, stored.MaxMessagesPerWindow);
        Assert.Equal(TimeSpan.FromSeconds(75), stored.RateLimitWindow);
        Assert.Null(stored.MaxAnonymousSessionsPerWindow);
        Assert.Null(stored.AnonymousSessionRateLimitWindow);
    }
}
