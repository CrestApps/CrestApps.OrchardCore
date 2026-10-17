namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// What an <see cref="ICommercePersonalDataContributor"/> is given to anonymize a person's records.
/// </summary>
public sealed class CommercePersonalDataAnonymizeContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommercePersonalDataAnonymizeContext"/> class.
    /// </summary>
    /// <param name="subject">The identified person.</param>
    /// <param name="replacementText">The text a removed name is replaced with, or <see langword="null"/> to clear it.</param>
    /// <param name="result">The result the contributors count their changes on.</param>
    public CommercePersonalDataAnonymizeContext(CommercePersonalDataSubject subject, string replacementText, CommercePersonalDataAnonymizeResult result)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(result);

        Subject = subject;
        ReplacementText = string.IsNullOrWhiteSpace(replacementText) ? null : replacementText;
        Result = result;
    }

    /// <summary>
    /// Gets the identified person.
    /// </summary>
    public CommercePersonalDataSubject Subject { get; }

    /// <summary>
    /// Gets the text a removed name is replaced with, or <see langword="null"/> when names are cleared.
    /// </summary>
    public string ReplacementText { get; }

    /// <summary>
    /// Gets the result the contributors count their changes on.
    /// </summary>
    public CommercePersonalDataAnonymizeResult Result { get; }

    /// <summary>
    /// Returns what a name field becomes: the replacement text, or <see langword="null"/> when the field was empty or
    /// names are cleared.
    /// </summary>
    /// <param name="name">The current name.</param>
    public string ReplaceName(string name)
        => string.IsNullOrEmpty(name) ? name : ReplacementText;

    /// <summary>
    /// Replaces the person's emails and names in free text, such as a note or an event message.
    /// </summary>
    /// <param name="text">The text to scrub.</param>
    public string Scrub(string text)
        => Subject.Scrub(text, ReplacementText);
}
