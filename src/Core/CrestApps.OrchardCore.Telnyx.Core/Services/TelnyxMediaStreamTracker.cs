using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Knows which of this node's calls have a live media stream, so a <c>streaming.failed</c> webhook can reach the
/// media session it is about.
/// </summary>
/// <remarks>
/// The webhook and the media session never share a call stack: the session is held open by the request Telnyx's
/// WebSocket arrived on, and the webhook is a request of its own. A stream that failed was otherwise only noticed when
/// its socket closed, and a socket broken somewhere between Telnyx and this server -- a proxy or a tunnel dropping it
/// -- may never close on this side: live, the assistant talked on to nobody for over a minute. A media session is a
/// live socket on one node, so this is per node, like the sockets themselves.
/// </remarks>
public sealed class TelnyxMediaStreamTracker
{
    private readonly ConcurrentDictionary<string, Action> _streams = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers the media stream of a call, and what to do when Telnyx reports it failed.
    /// </summary>
    /// <param name="callControlId">The call the stream carries.</param>
    /// <param name="onStreamFailed">Called when Telnyx reports the stream failed.</param>
    /// <returns>A registration that removes the stream when disposed.</returns>
    public IDisposable Track(string callControlId, Action onStreamFailed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callControlId);
        ArgumentNullException.ThrowIfNull(onStreamFailed);

        var key = callControlId.Trim();

        _streams[key] = onStreamFailed;

        return new Registration(this, key, onStreamFailed);
    }

    /// <summary>
    /// Tells the media session of a call that Telnyx reported its stream failed.
    /// </summary>
    /// <param name="callControlId">The call whose stream failed.</param>
    /// <returns><see langword="true"/> when a media session on this node carries the call.</returns>
    public bool NotifyStreamFailed(string callControlId)
    {
        if (string.IsNullOrWhiteSpace(callControlId) ||
            !_streams.TryGetValue(callControlId.Trim(), out var onStreamFailed))
        {
            return false;
        }

        onStreamFailed();

        return true;
    }

    private sealed class Registration : IDisposable
    {
        private readonly TelnyxMediaStreamTracker _tracker;
        private readonly string _key;
        private readonly Action _onStreamFailed;
        private int _disposed;

        public Registration(TelnyxMediaStreamTracker tracker, string key, Action onStreamFailed)
        {
            _tracker = tracker;
            _key = key;
            _onStreamFailed = onStreamFailed;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            // Only this registration's own entry: a later session for the same call may already have replaced it.
            _tracker._streams.TryRemove(new KeyValuePair<string, Action>(_key, _onStreamFailed));
        }
    }
}
