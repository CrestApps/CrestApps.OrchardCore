namespace CrestApps.Core.Omnichannel.Models;

/// <summary>
/// The kinds of value a subject field can hold.
/// </summary>
public enum SubjectFieldType
{
    /// <summary>
    /// A single line of text.
    /// </summary>
    Text,

    /// <summary>
    /// Several lines of text.
    /// </summary>
    MultilineText,

    /// <summary>
    /// A yes or no.
    /// </summary>
    Boolean,

    /// <summary>
    /// A number.
    /// </summary>
    Number,

    /// <summary>
    /// A date.
    /// </summary>
    Date,

    /// <summary>
    /// One of a fixed list of choices.
    /// </summary>
    Options,
}
