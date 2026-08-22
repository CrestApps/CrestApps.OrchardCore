namespace CrestApps.OrchardCore.Wizard.Contents;

/// <summary>
/// Identifies how the content types that a <c>WizardPart</c> may contain are resolved.
/// </summary>
public enum WizardPartSettingType
{
    /// <summary>
    /// No source is selected.
    /// </summary>
    None,

    /// <summary>
    /// The allowed step content types are listed explicitly.
    /// </summary>
    ContentTypes,

    /// <summary>
    /// The allowed step content types are every type that has one of the configured stereotypes.
    /// </summary>
    Stereotypes,
}
