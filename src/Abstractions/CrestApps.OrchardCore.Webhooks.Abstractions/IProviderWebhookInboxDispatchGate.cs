namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Lets a feature hold back the background retries of the provider webhook inbox while its own work must not run, such
/// as while Contact Center Voice is being disabled or replaced and its in-flight work drains.
/// </summary>
public interface IProviderWebhookInboxDispatchGate
{
    /// <summary>
    /// Admits a retry pass of the inbox.
    /// </summary>
    /// <returns>
    /// A lease to dispose when the pass ends, or <see langword="null"/> to skip the pass, which the next run of the
    /// background task tries again.
    /// </returns>
    IDisposable TryEnter();
}
