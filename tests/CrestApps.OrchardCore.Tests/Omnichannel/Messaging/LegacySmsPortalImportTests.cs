using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The import that carries a tenant from the SMS portal onto the workspace runs once. A step that throws takes every
/// later step with it, so a tenant would silently lose its routing and its agents' permissions.
/// </summary>
public sealed class LegacySmsPortalImportTests
{
    // Bug: a tenant whose endpoints included one with no SMS routing (a phone number, say) threw
    // KeyNotFoundException on that endpoint, which aborted the routing import and the role-permission import after it.
    [Fact]
    public async Task EndpointRouting_IsCarriedOver_WhenOtherEndpointsHaveNoSmsRouting()
    {
        var phone = new OmnichannelChannelEndpoint { ItemId = "phone", Channel = "Phone", Value = "+15550000001" };
        var sms = new OmnichannelChannelEndpoint { ItemId = "sms", Channel = "SMS", Value = "+15550000002" };
        sms.Properties["SmsEndpointRoutingSettings"] = new JsonObject { ["TargetType"] = "Queue", ["TargetId"] = "queue-1" };

        var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
        endpointManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([phone, sms]);

        var services = new ServiceCollection()
            .AddSingleton(endpointManager.Object)
            .AddSingleton(Mock.Of<ISession>())
            .BuildServiceProvider();

        await LegacySmsPortalImport.ImportEndpointRoutingAsync(services, NullLogger.Instance);

        Assert.True(sms.Properties.ContainsKey("MessagingEndpointRoutingSettings"));
        Assert.False(phone.Properties.ContainsKey("MessagingEndpointRoutingSettings"));
        endpointManager.Verify(manager => manager.UpdateAsync(sms, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        endpointManager.Verify(manager => manager.UpdateAsync(phone, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
