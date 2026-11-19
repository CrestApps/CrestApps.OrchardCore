using CrestApps.OrchardCore.Products.Models;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// The currencies the currency tools added, renamed or deleted in the current chat message. The currencies are one
/// document read from the cache until the message's service scope ends, so a change made a moment ago is not read back
/// yet; the tools read through this record instead, and a currency is changed once per message.
/// </summary>
internal sealed class CurrencyToolScope
{
    private readonly Dictionary<string, CurrencyEntry> _saved = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deleted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the currencies added or renamed in this message, by code.
    /// </summary>
    public IReadOnlyDictionary<string, CurrencyEntry> Saved
        => _saved;

    /// <summary>
    /// Gets the codes of the currencies deleted in this message.
    /// </summary>
    public IReadOnlySet<string> Deleted
        => _deleted;

    /// <summary>
    /// Gets whether a currency was changed in this message.
    /// </summary>
    /// <param name="code">The currency's code.</param>
    /// <returns><see langword="true"/> when it was added, renamed or deleted.</returns>
    public bool WasChanged(string code)
        => code is not null && (_saved.ContainsKey(code) || _deleted.Contains(code));

    /// <summary>
    /// Records a currency added or renamed.
    /// </summary>
    /// <param name="entry">The currency as saved.</param>
    public void RecordSaved(CurrencyEntry entry)
        => _saved[entry.Name] = entry;

    /// <summary>
    /// Records a currency deleted.
    /// </summary>
    /// <param name="code">The currency's code.</param>
    public void RecordDeleted(string code)
    {
        _saved.Remove(code);
        _deleted.Add(code);
    }

    /// <summary>
    /// Applies the changes of this message to the currencies read from the cache.
    /// </summary>
    /// <param name="stored">The currencies read from the cache.</param>
    /// <returns>The currencies as they are now.</returns>
    public IEnumerable<CurrencyEntry> Apply(IEnumerable<CurrencyEntry> stored)
        => (stored ?? [])
            .Where(entry => entry?.Name is not null && !_deleted.Contains(entry.Name) && !_saved.ContainsKey(entry.Name))
            .Concat(_saved.Values);
}
