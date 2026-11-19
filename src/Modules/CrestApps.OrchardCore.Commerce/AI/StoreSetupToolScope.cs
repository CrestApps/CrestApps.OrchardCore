namespace CrestApps.OrchardCore.Commerce.AI;

/// <summary>
/// The setup steps the store setup tools ran or skipped in the current chat message. Every tool call of one message
/// shares one service scope, and the settings a step reads to describe itself (the store's currency, its details) are
/// cached until that scope ends, so a step run a moment ago would still read as not done. The guide reports such a step
/// as run in this message instead.
/// </summary>
internal sealed class StoreSetupToolScope
{
    private readonly HashSet<string> _ran = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the steps whose quick action ran in this message.
    /// </summary>
    public IReadOnlySet<string> Ran
        => _ran;

    /// <summary>
    /// Records a step whose quick action ran.
    /// </summary>
    /// <param name="name">The step's name.</param>
    public void RecordRun(string name)
        => _ran.Add(name);
}
