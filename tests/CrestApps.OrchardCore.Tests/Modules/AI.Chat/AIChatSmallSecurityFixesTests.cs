using CrestApps.OrchardCore.AI.Chat.Controllers;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// SEC-A20 c and d.
/// </summary>
public sealed class AIChatSmallSecurityFixesTests
{
    // c: the realtime voice list answered anonymous callers and named the site's deployments.
    [Fact]
    public void RealtimeVoices_RequiresASignedInCaller()
    {
        Assert.NotNull(typeof(RealtimeController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
    }

    // d: a hub base with an unchecked HandleNotificationAction and no subclass was a trap for the next hub.
    [Fact]
    public void TheUnusedChatHubBase_IsGone()
    {
        Assert.Null(typeof(AIChatHub).Assembly.GetTypes().FirstOrDefault(type => type.Name.StartsWith("ChatHubBase", StringComparison.Ordinal)));
    }
}
