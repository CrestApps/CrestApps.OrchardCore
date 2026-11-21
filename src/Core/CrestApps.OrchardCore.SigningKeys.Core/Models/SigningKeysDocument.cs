using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.SigningKeys.Core.Models;

/// <summary>
/// The keys of the tenant's key sets, by key set name, newest last. Only ids, public halves, secret names and dates;
/// the key pairs are secrets of Orchard Core Secrets.
/// </summary>
public sealed class SigningKeysDocument : Document
{
    /// <summary>
    /// Gets or sets the keys of each key set.
    /// </summary>
    public Dictionary<string, List<SigningKeyRecord>> KeySets { get; set; } = new(StringComparer.Ordinal);
}
