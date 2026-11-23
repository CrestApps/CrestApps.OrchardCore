namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// Names shared by the Consent building block and the modules that use it.
/// </summary>
public static class ConsentConstants
{
    /// <summary>
    /// The path of the e-mail preferences page; the unsubscribe token follows it.
    /// </summary>
    public const string PreferencesPath = "email-preferences";

    /// <summary>
    /// The feature ids of the Consent module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The e-mail preferences page, unsubscribe links and address opt-outs.
        /// </summary>
        public const string Consent = "CrestApps.OrchardCore.Consent";
    }
}
