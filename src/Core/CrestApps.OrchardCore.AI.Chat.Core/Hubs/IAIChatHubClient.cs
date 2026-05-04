namespace CrestApps.OrchardCore.AI.Chat.Hubs;

/// <summary>
/// OrchardCore-specific extension of the framework <see cref="IAIChatHubClient"/>.
/// Currently identical — kept as a separate type so OC modules can extend it
/// independently without affecting the shared framework contract.
/// </summary>
public interface IAIChatHubClient : CrestApps.Core.AI.Chat.Hubs.IAIChatHubClient
{
}
