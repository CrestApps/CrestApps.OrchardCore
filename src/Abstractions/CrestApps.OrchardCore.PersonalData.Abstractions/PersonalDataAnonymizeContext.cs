namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// What a contributor anonymizes a person's records with.
/// </summary>
public sealed class PersonalDataAnonymizeContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalDataAnonymizeContext"/> class.
    /// </summary>
    /// <param name="subject">The person the request is about.</param>
    /// <param name="replacementText">The text names are replaced with, or <see langword="null"/> to clear them.</param>
    /// <param name="result">The result the changes are counted in.</param>
    public PersonalDataAnonymizeContext(PersonalDataSubject subject, string replacementText, PersonalDataAnonymizeResult result)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(result);

        Subject = subject;
        ReplacementText = string.IsNullOrWhiteSpace(replacementText) ? null : replacementText;
        Result = result;
    }

    /// <summary>
    /// Gets the person the request is about.
    /// </summary>
    public PersonalDataSubject Subject { get; }

    /// <summary>
    /// Gets the text names are replaced with, or <see langword="null"/> when they are cleared.
    /// </summary>
    public string ReplacementText { get; }

    /// <summary>
    /// Gets the result the changes are counted in.
    /// </summary>
    public PersonalDataAnonymizeResult Result { get; }

    /// <summary>
    /// Returns what a name is replaced with: the replacement text, or <see langword="null"/>; an empty name stays empty.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The replaced name.</returns>
    public string ReplaceName(string name)
        => string.IsNullOrEmpty(name) ? name : ReplacementText;

    /// <summary>
    /// Replaces the person's emails, phone numbers and names in free text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without the person's details.</returns>
    public string Scrub(string text)
        => Subject.Scrub(text, ReplacementText);
}
