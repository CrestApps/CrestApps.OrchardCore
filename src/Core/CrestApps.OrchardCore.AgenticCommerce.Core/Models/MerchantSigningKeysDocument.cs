using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Models;

/// <summary>
/// The store's signing keys: the one that signs now, and the ones that stopped signing but stay published so what they
/// signed can still be verified.
/// </summary>
public sealed class MerchantSigningKeysDocument : Document
{
    /// <summary>
    /// Gets or sets the keys, newest last.
    /// </summary>
    public List<MerchantSigningKey> Keys { get; set; } = [];
}
