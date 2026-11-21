namespace CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;

/// <summary>
/// The kind of input of a short text question.
/// </summary>
public enum ShortTextInputType
{
    /// <summary>
    /// Any text.
    /// </summary>
    Text,

    /// <summary>
    /// An e-mail address.
    /// </summary>
    Email,

    /// <summary>
    /// A web address.
    /// </summary>
    Url,

    /// <summary>
    /// A phone number.
    /// </summary>
    Phone,
}
