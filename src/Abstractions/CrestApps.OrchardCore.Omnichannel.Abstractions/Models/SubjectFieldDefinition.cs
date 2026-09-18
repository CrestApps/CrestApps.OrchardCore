namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// One field a subject of a given kind carries.
/// </summary>
/// <remarks>
/// This is the schema an automated conversation is allowed to write against. Without it the model
/// would be asked to author arbitrary structure, and a mis-heard phrase could end up anywhere on the
/// record.
/// </remarks>
public sealed class SubjectFieldDefinition
{
    /// <summary>
    /// Gets or sets the technical name, which is the key a value is stored under.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name to show. Falls back to <see cref="Name"/> when the host has nothing better.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the kind of value this field holds.
    /// </summary>
    public SubjectFieldType Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the field must have a value.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an automated conversation may set this field.
    /// </summary>
    /// <remarks>
    /// Off for anything a person has to be accountable for. A field the model may not touch is simply
    /// never offered to it.
    /// </remarks>
    public bool AllowAIUpdate { get; set; }

    /// <summary>
    /// Gets or sets the choices, when <see cref="Type"/> is <see cref="SubjectFieldType.Options"/>.
    /// </summary>
    public IList<string> Options { get; set; } = [];
}
