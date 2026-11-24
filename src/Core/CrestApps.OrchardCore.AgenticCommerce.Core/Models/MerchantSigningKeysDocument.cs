using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Models;

/// <summary>
/// The store's signing keys as earlier versions kept them, before they moved to the shared signing keys in Orchard Core
/// Secrets. Only <c>MerchantSigningKeyImportSource</c> reads it, to import the keys once; it is never written again.
/// </summary>
public sealed class MerchantSigningKeysDocument : Document
{
    /// <summary>
    /// Gets or sets the keys, newest last.
    /// </summary>
    public List<MerchantSigningKey> Keys { get; set; } = [];
}
