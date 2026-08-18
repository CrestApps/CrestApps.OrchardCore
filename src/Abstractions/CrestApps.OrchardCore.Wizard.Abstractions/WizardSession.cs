using System.Text.Json.Nodes;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// A durable, resumable wizard session. It is the reusable unit of work for any multi-step flow. Its
/// property bag carries feature-specific state contributed by wizard handlers.
/// </summary>
public sealed class WizardSession : Entity, IWizardFlowSession
{
    /// <summary>
    /// Gets or sets the unique identifier of the wizard session.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the wizard type discriminator that identifies which wizard this session belongs to.
    /// </summary>
    public string WizardType { get; set; }

    /// <summary>
    /// Gets or sets the optional identifier of the definition (for example the content item id) the wizard
    /// was started from. It is <see langword="null"/> for pure code wizards that have no backing definition.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the optional secondary identifier of the definition (for example a content item version
    /// id) the wizard was started from.
    /// </summary>
    public string DefinitionVersionId { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle state of the wizard.
    /// </summary>
    public WizardSessionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the session was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the session completed, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the authenticated user that owns the session, when applicable.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets the data collected for each completed step, keyed by step key.
    /// </summary>
    public JsonObject SavedSteps { get; init; } = [];

    /// <summary>
    /// Gets the ordered steps that make up this wizard.
    /// </summary>
    public IList<WizardStep> Steps { get; init; } = [];

    /// <summary>
    /// Gets or sets the key of the step the user is currently on.
    /// </summary>
    public string CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the client IP address captured for anonymous sessions, used to guard session ownership.
    /// </summary>
    public string IPAddress { get; set; }

    /// <summary>
    /// Gets or sets the user agent captured for anonymous sessions, used to guard session ownership.
    /// </summary>
    public string AgentInfo { get; set; }
}
