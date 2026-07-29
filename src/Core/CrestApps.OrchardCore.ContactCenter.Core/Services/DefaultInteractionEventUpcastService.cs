using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IInteractionEventUpcastService"/>. Registered upcasters
/// are applied one version step at a time until the event reaches the current schema version.
/// </summary>
public sealed class DefaultInteractionEventUpcastService : IInteractionEventUpcastService
{
    private readonly Dictionary<(string EventType, int FromVersion), IInteractionEventUpcaster> _upcasters = [];
    private readonly int _currentVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultInteractionEventUpcastService"/> class.
    /// </summary>
    /// <param name="upcasters">The registered upcasters.</param>
    /// <exception cref="InteractionEventUpcastException">
    /// Thrown when two upcasters claim the same event type and version step. Picking either one arbitrarily
    /// would make the converted payload depend on registration order.
    /// </exception>
    public DefaultInteractionEventUpcastService(IEnumerable<IInteractionEventUpcaster> upcasters)
        : this(upcasters, ContactCenterConstants.CurrentEventSchemaVersion)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultInteractionEventUpcastService"/> class targeting an
    /// explicit schema version. The version a release understands is a parameter of the conversion, not a
    /// property of the algorithm, so tests drive a chain of several steps without the shipped constant having
    /// to move first.
    /// </summary>
    /// <param name="upcasters">The registered upcasters.</param>
    /// <param name="currentVersion">The schema version events are converted up to.</param>
    internal DefaultInteractionEventUpcastService(IEnumerable<IInteractionEventUpcaster> upcasters, int currentVersion)
    {
        ArgumentNullException.ThrowIfNull(upcasters);

        _currentVersion = currentVersion;

        foreach (var upcaster in upcasters)
        {
            var key = (upcaster.EventType ?? string.Empty, upcaster.FromVersion);

            if (_upcasters.TryGetValue(key, out var existing))
            {
                throw new InteractionEventUpcastException(
                    $"Two Contact Center event upcasters convert '{key.Item1}' from schema version {upcaster.FromVersion}: '{existing.GetType().FullName}' and '{upcaster.GetType().FullName}'. Exactly one upcaster may own a version step.");
            }

            _upcasters[key] = upcaster;
        }
    }

    /// <inheritdoc/>
    public void Upcast(InteractionEvent interactionEvent)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        var current = _currentVersion;

        // A row written before the version was recorded reads as zero or less. It is the first version by
        // definition, so it enters the chain at the bottom rather than being treated as already current.
        var version = interactionEvent.SchemaVersion <= 0
            ? 1
            : interactionEvent.SchemaVersion;

        if (version > current)
        {
            throw new InteractionEventUpcastException(
                $"Contact Center event '{interactionEvent.ItemId}' of type '{interactionEvent.EventType}' was written at schema version {version}, but this release understands version {current}. It was written by a newer release; reading it here would deserialize a payload shape this code does not know into the shape it does, silently substituting defaults for whatever moved.");
        }

        if (version == current)
        {
            interactionEvent.SchemaVersion = current;

            return;
        }

        var payload = Parse(interactionEvent);

        while (version < current)
        {
            var upcaster = Resolve(interactionEvent.EventType, version)
                ?? throw new InteractionEventUpcastException(
                    $"No Contact Center event upcaster converts '{interactionEvent.EventType}' from schema version {version} to {version + 1}, so event '{interactionEvent.ItemId}' cannot be read by this release. Every version step between 1 and {current} needs an upcaster for each event type whose payload changed at that step.");

            payload = upcaster.Upcast(payload);
            version++;
        }

        interactionEvent.Data = payload is null
            ? null
            : payload.ToJsonString();

        interactionEvent.SchemaVersion = current;
    }

    private IInteractionEventUpcaster Resolve(string eventType, int fromVersion)
    {
        // An upcaster declared for the event type owns the step. A type-agnostic upcaster covers the steps no
        // event type claimed, which is what a change to a field every event carries looks like.
        if (!string.IsNullOrEmpty(eventType) &&
            _upcasters.TryGetValue((eventType, fromVersion), out var specific))
        {
            return specific;
        }

        return _upcasters.TryGetValue((string.Empty, fromVersion), out var universal)
            ? universal
            : null;
    }

    private static JsonNode Parse(InteractionEvent interactionEvent)
    {
        if (string.IsNullOrEmpty(interactionEvent.Data))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(interactionEvent.Data);
        }
        catch (JsonException exception)
        {
            throw new InteractionEventUpcastException(
                $"The payload of Contact Center event '{interactionEvent.ItemId}' of type '{interactionEvent.EventType}' is not valid JSON and cannot be converted from schema version {interactionEvent.SchemaVersion}.",
                exception);
        }
    }
}
