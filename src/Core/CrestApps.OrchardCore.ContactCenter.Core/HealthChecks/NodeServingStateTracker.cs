namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Tracks whether this node is currently able to serve, from the outcomes of real dependency probes observed
/// on this node.
/// </summary>
/// <remarks>
/// A single failed probe must never remove a node from rotation: dependency calls fail transiently all the
/// time, and reacting to one blip converts noise into lost capacity. Equally, a node whose own connection pool
/// is exhausted, whose DNS is stale, or whose TLS trust store has expired will fail every probe while its peers
/// stay healthy, and it must be drained.
/// <para>
/// Hysteresis separates the two: a node drains only after a run of consecutive failures, and returns only after
/// a run of consecutive successes. That also bounds the fleet-wide risk, because a shared outage still trips
/// every node — which is why the gate that consumes this tracker is opt-in and documented as requiring a load
/// balancer with fail-open behaviour.
/// </para>
/// <para>
/// Instances are per tenant shell and are mutated from concurrent probe requests, so all state transitions are
/// performed under a lock.
/// </para>
/// </remarks>
public sealed class NodeServingStateTracker
{
    private readonly int _consecutiveFailuresBeforeUnready;
    private readonly int _consecutiveSuccessesBeforeReady;
    private readonly Lock _gate = new();

    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private bool _isServing = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeServingStateTracker"/> class.
    /// </summary>
    /// <param name="consecutiveFailuresBeforeUnready">
    /// The number of consecutive failed probes required before the node reports that it cannot serve. Values
    /// below one are raised to one.
    /// </param>
    /// <param name="consecutiveSuccessesBeforeReady">
    /// The number of consecutive successful probes required before a draining node reports that it can serve
    /// again. Values below one are raised to one.
    /// </param>
    public NodeServingStateTracker(int consecutiveFailuresBeforeUnready, int consecutiveSuccessesBeforeReady)
    {
        _consecutiveFailuresBeforeUnready = Math.Max(1, consecutiveFailuresBeforeUnready);
        _consecutiveSuccessesBeforeReady = Math.Max(1, consecutiveSuccessesBeforeReady);
    }

    /// <summary>
    /// Gets a value indicating whether the node is currently considered able to serve.
    /// </summary>
    /// <remarks>
    /// A node starts able to serve, so a deployment is never blocked by a probe that has not run yet.
    /// </remarks>
    public bool IsServing
    {
        get
        {
            lock (_gate)
            {
                return _isServing;
            }
        }
    }

    /// <summary>
    /// Records the outcome of a dependency probe observed on this node and returns the resulting state.
    /// </summary>
    /// <param name="succeeded">Whether the probe succeeded.</param>
    /// <returns><see langword="true"/> when the node is considered able to serve after this outcome.</returns>
    public bool Record(bool succeeded)
    {
        lock (_gate)
        {
            if (succeeded)
            {
                _consecutiveFailures = 0;

                if (_isServing)
                {
                    _consecutiveSuccesses = 0;

                    return true;
                }

                _consecutiveSuccesses++;

                if (_consecutiveSuccesses >= _consecutiveSuccessesBeforeReady)
                {
                    _isServing = true;
                    _consecutiveSuccesses = 0;
                }

                return _isServing;
            }

            _consecutiveSuccesses = 0;

            if (!_isServing)
            {
                return false;
            }

            _consecutiveFailures++;

            if (_consecutiveFailures >= _consecutiveFailuresBeforeUnready)
            {
                _isServing = false;
                _consecutiveFailures = 0;
            }

            return _isServing;
        }
    }
}
