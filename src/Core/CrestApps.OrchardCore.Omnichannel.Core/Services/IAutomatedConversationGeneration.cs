namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// One in-flight automated reply. Disposing it releases the conversation, unless a newer generation has already
/// taken it over.
/// </summary>
public interface IAutomatedConversationGeneration : IDisposable
{
    /// <summary>
    /// Gets the token this generation runs under. It is cancelled when a newer inbound message supersedes it, or
    /// when the caller's own token is cancelled.
    /// </summary>
    CancellationToken Token { get; }
}
