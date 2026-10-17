using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What a pacing cycle decided, gathered from the events it published while it ran: how many records routing assigned
/// and how many it found no agent for, which attempts the compliance gate suppressed, and the calls it placed.
/// </summary>
/// <remarks>
/// <para>
/// One instance lives in each scope. The pacing schedulers call <see cref="Begin"/> before a cycle and read the
/// observation once it ends; <see cref="DialerPacingCycleObservingEventPublisher"/> hands it every event published on the
/// scope in between. Outside a cycle nothing is gathered.
/// </para>
/// <para>
/// A cycle abandoned at its time budget may still be publishing while the scheduler reads what it gathered, so every
/// member takes the same lock.
/// </para>
/// </remarks>
internal sealed class DialerPacingCycleObservation
{
    private readonly Lock _gate = new();
    private readonly Dictionary<DialerSuppressionReason, int> _suppressions = [];

    private bool _observing;
    private int _routingSucceeded;
    private int _routingNoAgent;
    private int _callsStarted;
    private string _lastPlacedInteractionId;
    private string _lastAssignedAgentId;
    private string _lastAssignedActivityItemId;

    /// <summary>
    /// Gets how many routing decisions in the cycle assigned a record to an agent.
    /// </summary>
    public int RoutingSucceeded
    {
        get
        {
            lock (_gate)
            {
                return _routingSucceeded;
            }
        }
    }

    /// <summary>
    /// Gets how many routing decisions in the cycle found no agent to take a record.
    /// </summary>
    public int RoutingNoAgent
    {
        get
        {
            lock (_gate)
            {
                return _routingNoAgent;
            }
        }
    }

    /// <summary>
    /// Gets how many calls the cycle placed.
    /// </summary>
    public int CallsStarted
    {
        get
        {
            lock (_gate)
            {
                return _callsStarted;
            }
        }
    }

    /// <summary>
    /// Gets how many attempts the compliance gate suppressed in the cycle.
    /// </summary>
    public int Suppressed
    {
        get
        {
            lock (_gate)
            {
                return _suppressions.Values.Sum();
            }
        }
    }

    /// <summary>
    /// Gets the interaction of the last call the cycle placed.
    /// </summary>
    public string LastPlacedInteractionId
    {
        get
        {
            lock (_gate)
            {
                return _lastPlacedInteractionId;
            }
        }
    }

    /// <summary>
    /// Gets the agent routing last assigned a record to in the cycle.
    /// </summary>
    public string LastAssignedAgentId
    {
        get
        {
            lock (_gate)
            {
                return _lastAssignedAgentId;
            }
        }
    }

    /// <summary>
    /// Gets the record routing last assigned to an agent in the cycle.
    /// </summary>
    public string LastAssignedActivityItemId
    {
        get
        {
            lock (_gate)
            {
                return _lastAssignedActivityItemId;
            }
        }
    }

    /// <summary>
    /// Starts gathering for a new cycle, forgetting whatever an earlier cycle on the scope gathered.
    /// </summary>
    public void Begin()
    {
        lock (_gate)
        {
            _observing = true;
            _routingSucceeded = 0;
            _routingNoAgent = 0;
            _callsStarted = 0;
            _lastPlacedInteractionId = null;
            _lastAssignedAgentId = null;
            _lastAssignedActivityItemId = null;
            _suppressions.Clear();
        }
    }

    /// <summary>
    /// Stops gathering; what was gathered stays readable.
    /// </summary>
    public void End()
    {
        lock (_gate)
        {
            _observing = false;
        }
    }

    /// <summary>
    /// Describes the suppressions the cycle saw, most frequent first, such as <c>DoNotCall x2, OutsideCallingWindow x1</c>.
    /// </summary>
    /// <returns>The description, or <see langword="null"/> when nothing was suppressed.</returns>
    public string DescribeSuppressions()
    {
        lock (_gate)
        {
            if (_suppressions.Count == 0)
            {
                return null;
            }

            return string.Join(
                ", ",
                _suppressions
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                    .Select(entry => $"{entry.Key} x{entry.Value}"));
        }
    }

    /// <summary>
    /// Gathers what an event published during the cycle says the cycle decided.
    /// </summary>
    /// <param name="interactionEvent">The published event.</param>
    public void Observe(InteractionEvent interactionEvent)
    {
        if (interactionEvent is null)
        {
            return;
        }

        lock (_gate)
        {
            if (!_observing)
            {
                return;
            }
        }

        switch (interactionEvent.EventType)
        {
            case ContactCenterConstants.Events.RoutingDecisionMade:
                ObserveRoutingDecision(DialerDecisionEvents.Read<DialerRoutingDecisionPayload>(interactionEvent));
                break;

            case ContactCenterConstants.Events.DialSuppressed:
                ObserveSuppression(DialerDecisionEvents.Read<DialerSuppressionPayload>(interactionEvent));
                break;

            case ContactCenterConstants.Events.DialerAttemptStarted:
                lock (_gate)
                {
                    _callsStarted++;
                    _lastPlacedInteractionId = interactionEvent.InteractionId;
                }

                break;
        }
    }

    private void ObserveRoutingDecision(DialerRoutingDecisionPayload decision)
    {
        if (decision is null)
        {
            return;
        }

        lock (_gate)
        {
            if (decision.Succeeded)
            {
                _routingSucceeded += decision.Occurrences;
                _lastAssignedAgentId = decision.SelectedAgentId;
                _lastAssignedActivityItemId = decision.ActivityItemId;
            }
            else
            {
                _routingNoAgent += decision.Occurrences;
            }
        }
    }

    private void ObserveSuppression(DialerSuppressionPayload suppression)
    {
        if (suppression is null)
        {
            return;
        }

        lock (_gate)
        {
            _suppressions[suppression.Reason] = _suppressions.GetValueOrDefault(suppression.Reason) + suppression.Occurrences;
        }
    }
}
