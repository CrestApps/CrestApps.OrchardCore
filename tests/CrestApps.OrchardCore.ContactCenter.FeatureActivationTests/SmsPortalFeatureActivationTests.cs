using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// The SMS Portal advertises a dependency on the agent directory, not on Contact Center work distribution.
/// These tests pin that a tenant which enables only the workspace can resolve and run the inbound pipeline, and
/// that push distribution pulls in work distribution by feature dependency rather than by hidden DI coupling.
/// </summary>
public sealed class SmsPortalFeatureActivationTests
{
    [Fact]
    public async Task FreshTenant_SmsPortalAlone_RoutesAnInboundMessageToAConversation()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "sms-portal-only",
            ProviderProfile = "none",
            Features =
            [
                SmsPortalConstants.Feature.Portal,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var endpointManager = services.GetRequiredService<IOmnichannelChannelEndpointManager>();
            var endpoint = await endpointManager.NewAsync();
            endpoint.Channel = OmnichannelConstants.Channels.Sms;
            endpoint.Value = "+15550001111";
            endpoint.DisplayText = "Support";

            await endpointManager.CreateAsync(endpoint);
        });

        // Act
        // The inbound chain must resolve and run without the Work Distribution feature; before the routed split
        // the always-registered push router dragged in IActivityQueueManager and every inbound SMS was dropped.
        var conversationId = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var handlers = services.GetServices<IOmnichannelEventHandler>();

            foreach (var handler in handlers)
            {
                await handler.HandleAsync(new OmnichannelEvent
                {
                    EventType = OmnichannelConstants.Events.SmsReceived,
                    Message = new OmnichannelMessage
                    {
                        Channel = OmnichannelConstants.Channels.Sms,
                        IsInbound = true,
                        ServiceAddress = "+15550001111",
                        CustomerAddress = "+15552223333",
                        Content = "Hello",
                    },
                });
            }

            var store = services.GetRequiredService<ISmsConversationStore>();
            var conversation = await store.FindByAddressesAsync("+15550001111", "+15552223333");

            return conversation?.ItemId;
        });

        // Assert
        Assert.False(string.IsNullOrEmpty(conversationId));
    }

    [Fact]
    public async Task FreshTenant_SmsPortalAlone_NeverPushAssignsAConversation()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "sms-portal-only-no-push",
            ProviderProfile = "none",
            Features =
            [
                SmsPortalConstants.Feature.Portal,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        // The contract resolves on every tenant, so callers can take it as a plain dependency; what the routed
        // feature changes is the answer, not whether there is one.
        var selectedAgentId = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => services.GetRequiredService<ISmsRoutingStrategy>().SelectAgentAsync("any-queue"));

        // Assert
        // Nobody is selected, so every conversation is pooled: a tenant that has not opted into push assignment
        // gets the shared-pool behaviour it configured.
        Assert.Null(selectedAgentId);
    }

    [Fact]
    public async Task FreshTenant_RoutedDistribution_EnablesWorkDistributionByDependency()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "sms-portal-routed",
            ProviderProfile = "none",
            Features =
            [
                SmsPortalConstants.Feature.RoutedDistribution,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var enabledFeatureIds = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var featuresManager = services.GetRequiredService<IShellFeaturesManager>();
            var features = await featuresManager.GetEnabledFeaturesAsync();

            return features.Select(feature => feature.Id).ToArray();
        });

        var routingStrategy = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => Task.FromResult(services.GetService<ISmsRoutingStrategy>()));

        // Assert
        Assert.Contains(ContactCenterConstants.Feature.Queues, enabledFeatureIds);
        Assert.Contains(SmsPortalConstants.Feature.Portal, enabledFeatureIds);
        Assert.NotNull(routingStrategy);
    }

    [Fact]
    public async Task FreshTenant_SmsPortalAlone_ResolvesTheConversationAuthorizationService()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "sms-portal-authorization",
            ProviderProfile = "none",
            Features =
            [
                SmsPortalConstants.Feature.Portal,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var resolved = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => Task.FromResult(services.GetService<ISmsConversationAuthorizationService>()));

        // Assert
        Assert.NotNull(resolved);
    }

    [Fact]
    public async Task SmsConversationOwnerType_IsReachableFromTheWorkspaceAssembly()
    {
        // A compile-time guard that the workspace models stay in the workspace assemblies rather than drifting
        // into the Omnichannel Management administration assembly this feature must not reference.
        Assert.Equal("Queue", SmsConversationOwnerType.Queue.ToString());
    }
}
