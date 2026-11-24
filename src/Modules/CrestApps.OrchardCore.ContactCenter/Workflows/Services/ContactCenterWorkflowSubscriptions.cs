using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using OrchardCore.Modules;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Services;

/// <summary>
/// Tells whether any enabled workflow listens for a Contact Center event type, so an event nobody listens for is not
/// handed to the workflow engine at all.
/// </summary>
public interface IContactCenterWorkflowSubscriptions
{
    /// <summary>
    /// Determines whether an enabled workflow has a <see cref="ContactCenterEvent"/> activity, start or not, for the
    /// event type, or one with no event type, which takes every event.
    /// </summary>
    /// <param name="eventType">The Contact Center event type.</param>
    /// <returns><see langword="true"/> when a workflow may start or resume on the event; otherwise <see langword="false"/>.</returns>
    Task<bool> IsSubscribedAsync(string eventType);
}

/// <summary>
/// Reads which Contact Center event types the enabled workflows listen for, from a per-tenant snapshot.
/// </summary>
/// <remarks>
/// Every Contact Center event, agent heartbeats included, used to be handed to the workflow engine: a deduplication
/// row was written, halted instances and the workflow types starting on the event were queried, and an instance was
/// saved even when the event was ignored. The workflow types are read once into a snapshot instead, refreshed when one
/// is created, updated or deleted on this server and at least every <see cref="ContactCenterWorkflowSubscriptionCache.Lifetime"/>,
/// so a change made on another server is picked up too.
/// </remarks>
public sealed class ContactCenterWorkflowSubscriptions : IContactCenterWorkflowSubscriptions
{
    private readonly IWorkflowTypeStore _workflowTypeStore;
    private readonly ContactCenterWorkflowSubscriptionCache _cache;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkflowSubscriptions"/> class.
    /// </summary>
    /// <param name="workflowTypeStore">The workflow types.</param>
    /// <param name="cache">The tenant's snapshot of the event types workflows listen for.</param>
    /// <param name="clock">The clock.</param>
    public ContactCenterWorkflowSubscriptions(
        IWorkflowTypeStore workflowTypeStore,
        ContactCenterWorkflowSubscriptionCache cache,
        IClock clock)
    {
        _workflowTypeStore = workflowTypeStore;
        _cache = cache;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> IsSubscribedAsync(string eventType)
    {
        var snapshot = _cache.Get(_clock.UtcNow);

        if (snapshot is null)
        {
            var version = _cache.Version;
            snapshot = ContactCenterWorkflowSubscriptionSnapshot.From(await _workflowTypeStore.ListAsync());
            _cache.Set(snapshot, version, _clock.UtcNow);
        }

        return snapshot.Takes(eventType);
    }
}

/// <summary>
/// The event types the enabled workflows listen for.
/// </summary>
public sealed class ContactCenterWorkflowSubscriptionSnapshot
{
    private readonly HashSet<string> _eventTypes;
    private readonly bool _takesEveryEvent;

    private ContactCenterWorkflowSubscriptionSnapshot(HashSet<string> eventTypes, bool takesEveryEvent)
    {
        _eventTypes = eventTypes;
        _takesEveryEvent = takesEveryEvent;
    }

    /// <summary>
    /// Reads the <see cref="ContactCenterEvent"/> activities of the enabled workflow types.
    /// </summary>
    /// <param name="workflowTypes">The workflow types.</param>
    /// <returns>The snapshot.</returns>
    public static ContactCenterWorkflowSubscriptionSnapshot From(IEnumerable<WorkflowType> workflowTypes)
    {
        var eventTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var takesEveryEvent = false;

        foreach (var workflowType in workflowTypes ?? [])
        {
            if (!workflowType.IsEnabled)
            {
                continue;
            }

            foreach (var activity in workflowType.Activities ?? [])
            {
                if (!string.Equals(activity.Name, nameof(ContactCenterEvent), StringComparison.Ordinal))
                {
                    continue;
                }

                var eventType = ReadEventType(activity);

                if (string.IsNullOrEmpty(eventType))
                {
                    takesEveryEvent = true;
                }
                else
                {
                    eventTypes.Add(eventType);
                }
            }
        }

        return new ContactCenterWorkflowSubscriptionSnapshot(eventTypes, takesEveryEvent);
    }

    /// <summary>
    /// Determines whether a workflow listens for the event type.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <returns><see langword="true"/> when a workflow listens for it; otherwise <see langword="false"/>.</returns>
    public bool Takes(string eventType)
        => _takesEveryEvent || (!string.IsNullOrEmpty(eventType) && _eventTypes.Contains(eventType));

    private static string ReadEventType(ActivityRecord activity)
    {
        var value = activity.Properties?[nameof(ContactCenterEvent.EventType)];

        return value is System.Text.Json.Nodes.JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text?.Trim()
            : null;
    }
}

/// <summary>
/// Holds a tenant's snapshot of the event types its workflows listen for.
/// </summary>
public sealed class ContactCenterWorkflowSubscriptionCache
{
    /// <summary>
    /// How long a snapshot is used before the workflow types are read again, so a change saved on another server is
    /// picked up.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private readonly Lock _lock = new();
    private ContactCenterWorkflowSubscriptionSnapshot _snapshot;
    private DateTime _expiresUtc;
    private long _version;

    /// <summary>
    /// Gets the current version, which changes each time the snapshot is invalidated.
    /// </summary>
    public long Version
    {
        get
        {
            lock (_lock)
            {
                return _version;
            }
        }
    }

    /// <summary>
    /// Gets the snapshot, or <see langword="null"/> when there is none or it expired.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The snapshot, or <see langword="null"/>.</returns>
    public ContactCenterWorkflowSubscriptionSnapshot Get(DateTime utcNow)
    {
        lock (_lock)
        {
            return _snapshot is not null && utcNow < _expiresUtc ? _snapshot : null;
        }
    }

    /// <summary>
    /// Keeps a snapshot read at a version, unless the snapshot was invalidated while it was read.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="version">The version read before the workflow types were.</param>
    /// <param name="utcNow">The current time.</param>
    public void Set(ContactCenterWorkflowSubscriptionSnapshot snapshot, long version, DateTime utcNow)
    {
        lock (_lock)
        {
            if (version != _version)
            {
                return;
            }

            _snapshot = snapshot;
            _expiresUtc = utcNow + Lifetime;
        }
    }

    /// <summary>
    /// Drops the snapshot, so the next event reads the workflow types again.
    /// </summary>
    public void Invalidate()
    {
        lock (_lock)
        {
            _version++;
            _snapshot = null;
        }
    }
}

/// <summary>
/// Drops the snapshot of the event types workflows listen for whenever a workflow type changes on this server.
/// </summary>
public sealed class ContactCenterWorkflowSubscriptionInvalidator : IWorkflowTypeEventHandler
{
    private readonly ContactCenterWorkflowSubscriptionCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkflowSubscriptionInvalidator"/> class.
    /// </summary>
    /// <param name="cache">The tenant's snapshot.</param>
    public ContactCenterWorkflowSubscriptionInvalidator(ContactCenterWorkflowSubscriptionCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public Task CreatedAsync(WorkflowTypeCreatedContext context)
    {
        _cache.Invalidate();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdatedAsync(WorkflowTypeUpdatedContext context)
    {
        _cache.Invalidate();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeletedAsync(WorkflowTypeDeletedContext context)
    {
        _cache.Invalidate();

        return Task.CompletedTask;
    }
}
