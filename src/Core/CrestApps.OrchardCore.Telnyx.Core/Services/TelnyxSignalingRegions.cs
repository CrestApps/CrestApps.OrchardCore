namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The Telnyx signaling regions a browser soft phone may register on.
/// </summary>
/// <remarks>
/// Telnyx resolves its signaling host by DNS geography and documents that the answer can be wrong -- their own
/// example is a client in India routed to Frankfurt rather than Chennai, with call latency to match. Pinning the
/// region rewrites the host the SDK connects to, which is why a tenant (and an agent, in the soft phone) can
/// choose one. This selects the SIGNALING edge only: Telnyx documents signaling and media as separate planes and
/// does not say how a browser leg's media gateway is picked, so whether media follows is for measurement to say.
/// </remarks>
public static class TelnyxSignalingRegions
{
    /// <summary>
    /// The regions Telnyx accepts, in the spelling its WebRTC SDK expects (its <c>Region</c> constant). There is
    /// deliberately no Middle East entry: Telnyx has no edge there and Europe is the nearest.
    /// </summary>
    public static readonly string[] All =
    [
        "us-west",
        "us-central",
        "us-east",
        "ca-central",
        "eu",
        "apac",
        "south-asia",
    ];

    /// <summary>
    /// Normalizes a configured region to one Telnyx knows, or <see langword="null"/> for "let Telnyx choose".
    /// </summary>
    /// <remarks>
    /// Anything Telnyx does not recognize is dropped rather than passed on, because the SDK turns a region into a
    /// signaling hostname: an unknown one resolves to nothing and the agent cannot register at all. Falling back
    /// to Telnyx's geo-routing leaves them on a working edge instead of a dead one.
    /// </remarks>
    /// <param name="value">The configured or stored region.</param>
    public static string Normalize(string value)
    {
        var region = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(region))
        {
            return null;
        }

        return Array.Exists(All, known => known == region)
            ? region
            : null;
    }
}
