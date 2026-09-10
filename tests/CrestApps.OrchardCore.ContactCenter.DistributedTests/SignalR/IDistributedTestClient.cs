namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.SignalR;

/// <summary>
/// Defines the provider-event callback used by the distributed SignalR test client.
/// </summary>
public interface IDistributedTestClient
{
    /// <summary>
    /// Receives a simulated provider event.
    /// </summary>
    /// <param name="eventId">The provider event identifier.</param>
    Task ProviderEvent(string eventId);
}
