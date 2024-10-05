namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// This class is stored in the session and keeps track of all subscription metadata.
/// </summary>
public sealed class SubscriptionCollectionMetadata
{
    public IList<SubscriptionMetadata> Subscriptions { get; set; }
}
