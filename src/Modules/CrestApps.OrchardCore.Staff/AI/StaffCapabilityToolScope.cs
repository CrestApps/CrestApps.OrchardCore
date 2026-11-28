namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Remembers, for the tool calls of one chat message, the capabilities the capability tools changed. Capabilities are
/// kept in one document that is read from the cache until the message's scope commits, so a capability added, changed or
/// deleted earlier in the same message is not seen by the capability service until the next reply; the tools say so
/// instead of answering as if nothing changed.
/// </summary>
internal sealed class StaffCapabilityToolScope
{
    private readonly HashSet<string> _changed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the capability keys changed earlier in this message.
    /// </summary>
    public IReadOnlyCollection<string> Changed => _changed;

    /// <summary>
    /// Records a change.
    /// </summary>
    /// <param name="name">The capability's key.</param>
    public void Record(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _changed.Add(name.Trim());
        }
    }

    /// <summary>
    /// Gets the sentence the tools add while changes of this message are not visible yet.
    /// </summary>
    /// <returns>The sentence, or <see langword="null"/> when nothing changed.</returns>
    public string Note()
        => _changed.Count == 0
            ? null
            : $"Capabilities changed earlier in this message ({string.Join(", ", _changed)}) show here, and can be given to staff, from the next reply.";
}
