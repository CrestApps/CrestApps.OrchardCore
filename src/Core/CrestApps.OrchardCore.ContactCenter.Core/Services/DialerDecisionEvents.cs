using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reads the decision events the dialer's activity is counted from.
/// </summary>
internal static class DialerDecisionEvents
{
    /// <summary>
    /// Reads the payload of an event, or returns <see langword="null"/> when it has none or one this release cannot read.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="interactionEvent">The event.</param>
    /// <returns>The payload, or <see langword="null"/>.</returns>
    public static T Read<T>(InteractionEvent interactionEvent)
        where T : class
    {
        if (interactionEvent is null)
        {
            return null;
        }

        try
        {
            return interactionEvent.GetData<T>();
        }
        catch (JsonException)
        {
            // A payload written by a release this one cannot read says nothing it could be counted by.
            return null;
        }
    }
}
