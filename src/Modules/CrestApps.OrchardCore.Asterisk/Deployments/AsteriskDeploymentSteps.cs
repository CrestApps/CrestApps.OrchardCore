using CrestApps.OrchardCore.Asterisk.Models;

namespace CrestApps.OrchardCore.Asterisk.Deployments;

/// <summary>
/// Names the recipe step that carries the Asterisk provider settings between environments, and the settings members
/// that hold data-protected secrets.
/// </summary>
public static class AsteriskDeploymentSteps
{
    /// <summary>
    /// The recipe step that carries the Asterisk provider settings.
    /// </summary>
    public const string Settings = "AsteriskSettings";

    /// <summary>
    /// The settings members that are stored data-protected, keyed by member name, with the protection purpose the
    /// settings screen uses for each. These members are never exported.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ProtectedMembers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(AsteriskSettings.Password)] = AsteriskConstants.ProtectorName,
        [nameof(AsteriskSettings.TurnSharedSecret)] = AsteriskConstants.ProtectorName,
        [nameof(AsteriskSettings.PjsipRealtimeConnectionString)] = AsteriskConstants.ProtectorName,
    };
}
