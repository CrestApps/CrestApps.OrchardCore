namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// One kind of subject a tenant has defined - what a conversation is about.
/// </summary>
/// <remarks>
/// A projection of whatever the host calls a subject type. The stable configuration of the flow
/// itself is read through the subject flow settings; this says what the record looks like.
/// </remarks>
public sealed class SubjectDefinition
{
    /// <summary>
    /// Gets or sets the technical name, which is what a subject and a subject action store to say what
    /// kind of work they belong to.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name to show. Falls back to <see cref="Name"/> when the host has nothing better.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the fields a subject of this kind carries.
    /// </summary>
    public IList<SubjectFieldDefinition> Fields { get; set; } = [];
}
