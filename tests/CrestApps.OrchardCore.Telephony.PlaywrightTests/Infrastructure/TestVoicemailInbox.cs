using System.Collections.Concurrent;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// The voicemails the harness lists on the soft phone's Voicemail tab, and the delete endpoint's answers for them.
/// </summary>
public sealed class TestVoicemailInbox
{
    private readonly ConcurrentDictionary<string, TelephonyInteraction> _voicemails = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _refused = new(StringComparer.Ordinal);
    private int _concurrentDeletes;

    /// <summary>
    /// Gets the most deletes that were in flight at the same time.
    /// </summary>
    public int MaxConcurrentDeletes { get; private set; }

    /// <summary>
    /// Adds a voicemail to the inbox.
    /// </summary>
    /// <param name="interactionId">The voicemail's inbox identifier.</param>
    /// <param name="from">The caller's number.</param>
    /// <param name="refuseWithRedirect">Whether the delete endpoint refuses it the way the site's cookie
    /// authentication used to: with a redirect to a page that answers 200.</param>
    public void Add(string interactionId, string from, bool refuseWithRedirect = false)
    {
        _voicemails[interactionId] = new TelephonyInteraction
        {
            InteractionId = interactionId,
            CallId = "call-" + interactionId,
            ProviderName = "InMemory",
            From = from,
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.Missed,
            IsVoicemail = true,
            StartedUtc = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc).AddMinutes(_voicemails.Count),
        };

        if (refuseWithRedirect)
        {
            _refused[interactionId] = true;
        }
    }

    /// <summary>
    /// Gets the voicemails still in the inbox.
    /// </summary>
    public IReadOnlyList<TelephonyInteraction> List()
    {
        return [.. _voicemails.Values.OrderByDescending(voicemail => voicemail.StartedUtc)];
    }

    /// <summary>
    /// Deletes a voicemail, answering as the server would.
    /// </summary>
    /// <param name="interactionId">The voicemail's inbox identifier.</param>
    public async Task<bool> TryDeleteAsync(string interactionId)
    {
        var inFlight = Interlocked.Increment(ref _concurrentDeletes);
        MaxConcurrentDeletes = Math.Max(MaxConcurrentDeletes, inFlight);

        try
        {
            // Long enough that deletes sent together would overlap.
            await Task.Delay(50);

            return !_refused.ContainsKey(interactionId) && _voicemails.TryRemove(interactionId, out _);
        }
        finally
        {
            Interlocked.Decrement(ref _concurrentDeletes);
        }
    }
}
