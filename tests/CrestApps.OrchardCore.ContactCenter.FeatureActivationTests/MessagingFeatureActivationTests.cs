using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// The messaging workspace advertises a dependency on the agent directory, not on Contact Center work
/// distribution, and carries no channel of its own. These tests pin that a tenant which enables only the SMS channel
/// (and so the workspace) can resolve and run the inbound pipeline, that the workspace boots without any channel, and
/// that push distribution pulls in work distribution by feature dependency rather than by hidden DI coupling.
/// </summary>
public sealed class MessagingFeatureActivationTests
{
    [Fact]
    public async Task FreshTenant_SmsChannelAlone_RoutesAnInboundMessageToAConversation()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "messaging-only",
            ProviderProfile = "none",
            Features =
            [
                MessagingConstants.Feature.Sms,
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

            var store = services.GetRequiredService<IMessagingConversationStore>();
            var conversation = await store.FindByAddressesAsync(OmnichannelConstants.Channels.Sms, "+15550001111", "+15552223333");

            return conversation?.ItemId;
        });

        // Assert
        Assert.False(string.IsNullOrEmpty(conversationId));
    }

    [Fact]
    public async Task FreshTenant_WorkspaceAlone_NeverPushAssignsAConversation()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "messaging-only-no-push",
            ProviderProfile = "none",
            Features =
            [
                MessagingConstants.Feature.Workspace,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        // The contract resolves on every tenant, so callers can take it as a plain dependency; what the routed
        // feature changes is the answer, not whether there is one.
        var selectedAgentId = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => services.GetRequiredService<IMessagingRoutingStrategy>().SelectAgentAsync("any-queue"));

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
            Id = "messaging-routed",
            ProviderProfile = "none",
            Features =
            [
                MessagingConstants.Feature.RoutedDistribution,
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
            services => Task.FromResult(services.GetService<IMessagingRoutingStrategy>()));

        // Assert
        Assert.Contains(ContactCenterConstants.Feature.Queues, enabledFeatureIds);
        Assert.Contains(MessagingConstants.Feature.Workspace, enabledFeatureIds);
        Assert.NotNull(routingStrategy);
    }

    [Fact]
    public async Task FreshTenant_WorkspaceAlone_ResolvesTheConversationAuthorizationService()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "messaging-authorization",
            ProviderProfile = "none",
            Features =
            [
                MessagingConstants.Feature.Workspace,
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var resolved = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => Task.FromResult(services.GetService<IMessagingConversationAuthorizationService>()));

        // Assert
        Assert.NotNull(resolved);
    }

    [Fact]
    public async Task FreshTenant_WorkspaceAlone_BootsWithNoChannel_AndTheSmsChannelAddsOne()
    {
        // The workspace is channel-agnostic: enabled on its own it must boot and offer no channel, and each channel
        // feature is what adds one. SMS is the first; email, WhatsApp and the like are further features.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var workspaceOnly = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "messaging-no-channel",
            ProviderProfile = "none",
            Features = [MessagingConstants.Feature.Workspace],
        });

        var withSms = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "messaging-sms-channel",
            ProviderProfile = "none",
            Features = [MessagingConstants.Feature.Sms],
        });

        var channelsWithoutSms = await host.ExecuteInTenantScopeAsync(
            workspaceOnly,
            services => Task.FromResult(services.GetRequiredService<IMessagingChannelResolver>().GetAll().Select(channel => channel.Name).ToArray()));

        var channelsWithSms = await host.ExecuteInTenantScopeAsync(
            withSms,
            services => Task.FromResult(services.GetRequiredService<IMessagingChannelResolver>().GetAll().Select(channel => channel.Name).ToArray()));

        Assert.Empty(channelsWithoutSms);
        Assert.Equal([OmnichannelConstants.Channels.Sms], channelsWithSms);
    }

    [Fact]
    public async Task SmsConversationOwnerType_IsReachableFromTheWorkspaceAssembly()
    {
        // A compile-time guard that the workspace models stay in the workspace assemblies rather than drifting
        // into the Omnichannel Management administration assembly this feature must not reference.
        Assert.Equal("Queue", ConversationOwnerType.Queue.ToString());
    }
}
