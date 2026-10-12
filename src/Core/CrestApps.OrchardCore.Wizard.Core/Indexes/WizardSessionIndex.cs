using CrestApps.OrchardCore.Wizard;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Wizard.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="WizardSession"/>.
/// </summary>
public sealed class WizardSessionIndex : MapIndex
{
    /// <summary>
    /// The wizard session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The wizard type discriminator.
    /// </summary>
    public string WizardType { get; set; }

    /// <summary>
    /// The optional identifier of the definition the wizard was started from.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// The optional secondary identifier of the definition the wizard was started from.
    /// </summary>
    public string DefinitionVersionId { get; set; }

    /// <summary>
    /// The owning user id, when the session belongs to an authenticated user.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// The lifecycle state of the wizard.
    /// </summary>
    public WizardSessionStatus Status { get; set; }

    /// <summary>
    /// The UTC time the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC time the session was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// The UTC time the session completed, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
