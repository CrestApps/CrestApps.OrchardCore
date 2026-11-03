using System.Collections.Concurrent;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <inheritdoc cref="IAnsweringMachineSignals"/>
/// <remarks>
/// <para>
/// A tenant singleton holding the signals in memory, so every request on this node sees the same ones at once.
/// That is enough because the session the signal is for runs on the node that received the "call answered"
/// webhook, and a provider's later webhooks for that call have to reach the same node for the live media to work at
/// all (see the Voice Media documentation on node affinity). A detection delivered to another node is not seen by
/// the session; the activity is still marked, and the call is still concluded as having reached voicemail.
/// </para>
/// <para>
/// A call is forgotten when it hangs up and when the session holding it ends. A call that was never forgotten --
/// its hangup was never reported -- is dropped once it is older than any call could be.
/// </para>
/// </remarks>
internal sealed class AnsweringMachineSignals : IAnsweringMachineSignals
{
    /// <summary>
    /// How long a call's signals are kept when nothing forgets them.
    /// </summary>
    internal static readonly TimeSpan Retention = TimeSpan.FromHours(4);

    private readonly ConcurrentDictionary<string, AnsweringMachineSignal> _calls = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnsweringMachineSignals"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public AnsweringMachineSignals(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public void MachineDetected(string providerName, string providerCallId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCallId);

        GetOrAdd(providerName, providerCallId).ReportMachineDetected();
    }

    /// <inheritdoc/>
    public void GreetingEnded(string providerName, string providerCallId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCallId);

        GetOrAdd(providerName, providerCallId).ReportGreetingEnded();
    }

    /// <inheritdoc/>
    public void PhoneMenuReached(string providerName, string providerCallId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCallId);

        GetOrAdd(providerName, providerCallId).ReportPhoneMenuReached();
    }

    /// <inheritdoc/>
    public AnsweringMachineSignal Watch(string providerName, string providerCallId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCallId);

        return GetOrAdd(providerName, providerCallId);
    }

    /// <inheritdoc/>
    public void Forget(string providerName, string providerCallId)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(providerCallId))
        {
            return;
        }

        _calls.TryRemove(Key(providerName, providerCallId), out _);
    }

    /// <summary>
    /// Gets the number of calls whose signals are held right now.
    /// </summary>
    internal int Count => _calls.Count;

    // Created on the first report or the first watch, whichever comes first, so neither order loses the signal.
    private AnsweringMachineSignal GetOrAdd(string providerName, string providerCallId)
    {
        var now = _clock.UtcNow;

        DropForgottenCalls(now);

        return _calls.GetOrAdd(Key(providerName, providerCallId), _ => new AnsweringMachineSignal(now));
    }

    // A hangup that was never reported leaves a call nobody forgets. The set only ever holds the calls that are up,
    // so a walk over it is short.
    private void DropForgottenCalls(DateTime now)
    {
        foreach (var call in _calls)
        {
            if (now - call.Value.CreatedUtc > Retention)
            {
                _calls.TryRemove(call);
            }
        }
    }

    private static string Key(string providerName, string providerCallId)
        => providerName + "\n" + providerCallId;
}
