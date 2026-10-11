namespace CrestApps.OrchardCore.Wizard.Contents;

/// <summary>
/// Well-known identifiers for the content-driven wizard feature.
/// </summary>
public static class ContentWizardConstants
{
    /// <summary>
    /// The wizard type used by every wizard that is built from a content item's wizard part. Individual
    /// wizards are distinguished by their definition content item id.
    /// </summary>
    public const string WizardType = "Content";

    /// <summary>
    /// The session property key that stores the completion policy resolved from the wizard part settings so
    /// the completion handler can apply it without reloading the authored content item.
    /// </summary>
    public const string CompletionPolicyPropertyKey = "ContentWizardCompletionPolicy";
}
