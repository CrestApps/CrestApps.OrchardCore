namespace CrestApps.OrchardCore.Wizard.Contents;

/// <summary>
/// The <see cref="IWizardDefinition"/> that registers the content-driven wizard type, so the public wizard
/// host accepts a wizard started from a content item's wizard part.
/// </summary>
public sealed class ContentWizardDefinition : IWizardDefinition
{
    /// <inheritdoc/>
    public string WizardType => ContentWizardConstants.WizardType;

    /// <inheritdoc/>
    public string DisplayName => "Content wizard";

    /// <inheritdoc/>
    public bool RequiresAuthenticatedUser => false;
}
