namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// Finds, exports and anonymizes the personal data one module keeps about a person. Every module that stores personal
/// data registers one, so a privacy request reaches all of it through <see cref="IPersonalDataService"/>.
/// </summary>
public interface IPersonalDataContributor
{
    /// <summary>
    /// Gets the order the contributor runs in; lower runs first. A contributor whose records lead to the person's other
    /// records (for example the email a guest checkout was made with) runs before the ones that erase those links.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Gets a value indicating whether the contributor takes part when a retention window runs out, such as the nightly
    /// expiry of inactive guests. It is <see langword="false"/> unless the contributor opts in, so a retention expiry
    /// only reaches the records it was built for; an explicit erasure request always runs every contributor.
    /// </summary>
    bool TakesPartInRetentionExpiry => false;

    /// <summary>
    /// Adds what the contributor's records know about the person to the subject (more user IDs, emails, phone numbers or
    /// names), so the other contributors find their records too. It runs until nothing new is found, before anything is
    /// exported or anonymized.
    /// </summary>
    /// <param name="subject">The person the request is about.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Adds the person's records to the export.
    /// </summary>
    /// <param name="context">The export context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes or replaces the personal data in the person's records, keeping the records a business must keep (such as
    /// orders and time worked), and counts what changed in the result.
    /// </summary>
    /// <param name="context">The anonymize context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default);
}
