using System.Net;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Interprets the HTTP responses a telephony provider receives from its remote control plane, centralizing the
/// provider-agnostic decisions every REST-backed provider otherwise re-implements.
/// </summary>
public static class TelephonyProviderResponse
{
    /// <summary>
    /// Determines whether an HTTP status code leaves a call's real outcome ambiguous, so the caller must treat the
    /// operation as indeterminate rather than as a definite success or failure. Request timeouts, throttling
    /// responses, and server-side faults are ambiguous because the remote side may have applied the change even
    /// though it did not acknowledge it.
    /// </summary>
    /// <param name="statusCode">The status code returned by the provider's control plane.</param>
    /// <returns><see langword="true"/> when the outcome is ambiguous; otherwise, <see langword="false"/>.</returns>
    public static bool IsAmbiguousStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)statusCode >= 500;
    }

    /// <summary>
    /// Resolves the normalized <see cref="CallDirection"/> from a provider-reported direction string, treating the
    /// literal <c>inbound</c> (case-insensitively) as an inbound call and every other value as outbound.
    /// </summary>
    /// <param name="direction">The direction string reported by the provider.</param>
    /// <returns>The normalized call direction.</returns>
    public static CallDirection ResolveDirection(string direction)
    {
        return string.Equals(direction?.Trim(), "inbound", StringComparison.OrdinalIgnoreCase)
            ? CallDirection.Inbound
            : CallDirection.Outbound;
    }
}
