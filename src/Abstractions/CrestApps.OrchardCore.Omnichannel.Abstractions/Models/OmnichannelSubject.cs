using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// What one conversation with a contact is about.
/// </summary>
/// <remarks>
/// A projection, like <see cref="OmnichannelContact"/>. In a host that stores subjects as content
/// items this is built on read; the fields carry whatever that host's record holds.
/// </remarks>
public sealed class OmnichannelSubject
{
    /// <summary>
    /// Gets or sets the identifier the rest of the suite stores against activities.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the <see cref="SubjectDefinition"/> this subject is an instance of.
    /// </summary>
    public string DefinitionName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the contact this subject is about.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the name to show.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the field values, keyed by <see cref="SubjectFieldDefinition.Name"/>.
    /// </summary>
    public JsonObject Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets whatever else the host carries on this subject.
    /// </summary>
    public JsonObject Properties { get; set; } = [];
}
