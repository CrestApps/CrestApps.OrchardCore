namespace CrestApps.OrchardCore.Workforce.ContactCenter;

/// <summary>
/// The constants of contact center workforce management.
/// </summary>
public static class WorkforceContactCenterConstants
{
    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Workforce for Contact Center: a worker for every agent.
        /// </summary>
        public const string ContactCenter = "CrestApps.OrchardCore.Workforce.ContactCenter";

        /// <summary>
        /// Clocks agents in and out when they sign in and out of the contact center.
        /// </summary>
        public const string SignInPunches = "CrestApps.OrchardCore.Workforce.ContactCenter.SignInPunches";

        /// <summary>
        /// Real-time and historical adherence.
        /// </summary>
        public const string Adherence = "CrestApps.OrchardCore.Workforce.Adherence";
    }
}
