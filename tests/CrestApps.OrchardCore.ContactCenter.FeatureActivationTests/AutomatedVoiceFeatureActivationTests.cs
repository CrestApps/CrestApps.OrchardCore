using CrestApps.OrchardCore.Omnichannel.Voice;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// The automated voice conversation is provider-neutral, so a tenant can enable it without enabling any
/// telephony provider. These pin that the feature stands on its own: the loop resolves, and a call arriving for
/// a provider that supplies no audio is declined quietly rather than throwing or starting a conversation nobody
/// can hear.
/// </summary>
public sealed class AutomatedVoiceFeatureActivationTests
{
    [Fact]
    public async Task FreshTenant_AutomatedVoiceAlone_ResolvesTheConversationLoop()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "automated-voice-only",
            ProviderProfile = "none",
            Features =
            [
                OmnichannelVoiceConstants.Feature.Area,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var resolved = await host.ExecuteInTenantScopeAsync(tenant, services =>
            Task.FromResult(services.GetService<IVoiceAgentConversationLoop>() is not null));

        // Assert
        Assert.True(resolved);
    }

    [Fact]
    public async Task WithNoProviderSupplyingAudio_AnEventIsDeclinedQuietly()
    {
        // Arrange
        // Nothing on this tenant can speak, so the only correct outcome is to leave the call alone. Throwing here
        // would fail the provider's webhook and have it redelivered forever.
        var profile = new ContactCenterTenantProfile
        {
            Id = "automated-voice-no-media",
            ProviderProfile = "none",
            Features =
            [
                OmnichannelVoiceConstants.Feature.Area,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var exception = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var loop = services.GetRequiredService<IVoiceAgentConversationLoop>();

            return await Record.ExceptionAsync(() => loop.HandleAsync(new VoiceAgentEvent
            {
                Kind = VoiceAgentEventKind.Answered,
                ProviderCallId = "call-1",
                ProviderName = "NoSuchProvider",
                ActivityId = "activity-1",
            }, TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task TheMediaResolver_ComesFromTelephony_NotFromAnyProvider()
    {
        // Arrange
        // The loop asks a resolver which provider carries a call. That resolver is part of Telephony, which the
        // feature depends on, so it must be there with no provider module installed.
        var profile = new ContactCenterTenantProfile
        {
            Id = "automated-voice-resolver",
            ProviderProfile = "none",
            Features =
            [
                OmnichannelVoiceConstants.Feature.Area,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var hasResolver = await host.ExecuteInTenantScopeAsync(tenant, services =>
            Task.FromResult(services.GetService<IVoiceAgentMediaProviderResolver>() is not null));

        // Assert
        Assert.True(hasResolver);
    }
}
