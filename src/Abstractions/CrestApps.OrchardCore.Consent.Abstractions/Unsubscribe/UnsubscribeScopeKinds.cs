namespace CrestApps.OrchardCore.Consent.Unsubscribe;

/// <summary>
/// Scope kinds the Consent building block handles itself. Other kinds belong to the modules that register them.
/// </summary>
public static class UnsubscribeScopeKinds
{
    /// <summary>
    /// All marketing e-mail to the address; recorded as a <see cref="ConsentPurposes.Marketing"/> opt-out.
    /// </summary>
    public const string AllMarketing = "AllMarketing";
}
