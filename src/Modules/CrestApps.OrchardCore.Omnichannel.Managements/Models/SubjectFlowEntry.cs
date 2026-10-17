using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// A subject content type and the configuration of its flow, as listed on the Subject Flows page.
/// </summary>
public sealed class SubjectFlowEntry
{
    /// <summary>
    /// Gets or sets the content type name.
    /// </summary>
    public string ContentTypeName { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the primary communication direction configured for the subject.
    /// </summary>
    public SubjectDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the inbound interaction type configured for the subject.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the inbound channel configured for the subject.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether subject actions have been defined.
    /// </summary>
    public bool HasActions { get; set; }
}
