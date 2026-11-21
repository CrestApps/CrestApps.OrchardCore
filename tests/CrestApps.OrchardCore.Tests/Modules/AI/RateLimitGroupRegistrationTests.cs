using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.A2A;
using CrestApps.OrchardCore.AI.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Tests.Modules.AI;

/// <summary>
/// The rate-limit groups SEC-A5, SEC-A18 and SEC-A20 f put on public or model-calling routes have a built-in limit when
/// Orchard Core's Rate Limits feature is enabled.
/// </summary>
public sealed class RateLimitGroupRegistrationTests
{
    [Theory]
    [InlineData(typeof(ApiChatRateLimitsStartup), "ai-completion")]
    [InlineData(typeof(ChatSessionDocumentsRateLimitsStartup), "ai-chat-documents")]
    [InlineData(typeof(A2AHostRateLimitsStartup), "a2a-host")]
    [InlineData(typeof(CrestApps.OrchardCore.Wizard.RateLimitsStartup), "wizard")]
    public void TheGroup_HasABuiltInLimit(Type startupType, string group)
    {
        var services = new ServiceCollection();
        ((IStartup)Activator.CreateInstance(startupType)).ConfigureServices(services);

        var limits = services.BuildServiceProvider().GetRequiredService<IOptions<RateLimitsOptions>>().Value.GroupRateLimits;

        Assert.Single(limits, limit => limit.GroupName == group);
    }
}
