using System.Text.Json.Nodes;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The read/write surface of a wizard session that the flow navigation and step drivers operate on.
/// </summary>
public interface IWizardFlowSession : IEntity
{
    /// <summary>
    /// Gets or sets the unique identifier of the wizard session.
    /// </summary>
    string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the wizard type discriminator that identifies which wizard this session belongs to.
    /// </summary>
    string WizardType { get; set; }

    /// <summary>
    /// Gets or sets the key of the step the user is currently on.
    /// </summary>
    string CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle state of the wizard.
    /// </summary>
    WizardSessionStatus Status { get; set; }

    /// <summary>
    /// Gets the ordered steps that make up this wizard.
    /// </summary>
    IList<WizardStep> Steps { get; }

    /// <summary>
    /// Gets the data collected for each completed step, keyed by step key.
    /// </summary>
    JsonObject SavedSteps { get; }

    /// <summary>
    /// Gets or sets the identifier of the authenticated user that owns the session, when applicable.
    /// </summary>
    string OwnerId { get; set; }
}
