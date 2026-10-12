namespace CrestApps.OrchardCore.Wizard.Core.Models;

/// <summary>
/// Determines what a content-driven wizard does with the response content items a visitor filled in once the
/// wizard completes.
/// </summary>
public enum WizardCompletionPolicy
{
    /// <summary>
    /// The response content items are kept only in the wizard session and are not persisted as content items.
    /// A workflow or a custom handler is expected to consume them.
    /// </summary>
    None = 0,

    /// <summary>
    /// The response content items are created as drafts.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// The response content items are created and published.
    /// </summary>
    Publish = 2,
}
