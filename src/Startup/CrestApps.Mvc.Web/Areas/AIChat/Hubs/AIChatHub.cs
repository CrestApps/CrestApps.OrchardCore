using CrestApps.AI.Chat.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.Mvc.Web.Areas.AIChat.Hubs;

/// <summary>
/// MVC-specific AI chat hub. Inherits all behavior from <see cref="AIChatHubCore"/>.
/// Uses constructor-injected services directly (no ShellScope needed).
/// </summary>
[Authorize]
public sealed class AIChatHub : AIChatHubCore<IAIChatHubClient>
{
    public AIChatHub(
        IServiceProvider services,
        TimeProvider timeProvider,
        ILogger<AIChatHub> logger)
        : base(services, timeProvider, logger)
    {
    }
}
