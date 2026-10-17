using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Records the calls held for shutdown and the hand-on work tracked, and lets a test stop the node.
/// </summary>
internal sealed class FakeAIVoiceCallShutdown : IAIVoiceCallShutdown
{
    private readonly CancellationTokenSource _stopping = new();
    private int _liveCalls;

    public CancellationToken Stopping => _stopping.Token;

    /// <summary>
    /// Gets the number of calls currently held.
    /// </summary>
    public int LiveCalls => Volatile.Read(ref _liveCalls);

    /// <summary>
    /// Gets the most calls that were held at once.
    /// </summary>
    public int MostLiveCalls { get; private set; }

    /// <summary>
    /// Gets the hand-on work that was tracked.
    /// </summary>
    public List<Task> HandOns { get; } = [];

    /// <summary>
    /// Stops the node, as the host does when it begins to shut down.
    /// </summary>
    public void Stop() => _stopping.Cancel();

    public IDisposable HoldCall()
    {
        MostLiveCalls = Math.Max(MostLiveCalls, Interlocked.Increment(ref _liveCalls));

        return new Release(this);
    }

    public void TrackHandOn(Task work) => HandOns.Add(work);

    private sealed class Release : IDisposable
    {
        private FakeAIVoiceCallShutdown _owner;

        public Release(FakeAIVoiceCallShutdown owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);

            if (owner is not null)
            {
                Interlocked.Decrement(ref owner._liveCalls);
            }
        }
    }
}
