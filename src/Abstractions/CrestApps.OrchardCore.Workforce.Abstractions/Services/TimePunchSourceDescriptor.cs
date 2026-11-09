using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A punch source a module adds, with the words screens use for it.
/// </summary>
public sealed class TimePunchSourceDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimePunchSourceDescriptor"/> class.
    /// </summary>
    /// <param name="name">The source name punches are stored with.</param>
    /// <param name="displayName">A short name, such as "contact center", for lists and timesheets.</param>
    /// <param name="phrase">The source in a sentence, such as "the contact center", for messages.</param>
    /// <param name="hint">A hint shown under the source where a time clock policy allows it, or <see langword="null"/>.</param>
    public TimePunchSourceDescriptor(string name, LocalizedString displayName, LocalizedString phrase, LocalizedString hint = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        DisplayName = displayName;
        Phrase = phrase;
        Hint = hint;
    }

    /// <summary>
    /// Gets the hint shown under the source where a time clock policy allows it.
    /// </summary>
    public LocalizedString Hint { get; }

    /// <summary>
    /// Gets the source name punches are stored with.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a short name for lists and timesheets.
    /// </summary>
    public LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the source in a sentence, for messages.
    /// </summary>
    public LocalizedString Phrase { get; }
}
