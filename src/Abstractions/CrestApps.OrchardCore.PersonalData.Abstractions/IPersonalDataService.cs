namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// Runs every registered <see cref="IPersonalDataContributor"/> for a privacy request.
/// </summary>
public interface IPersonalDataService
{
    /// <summary>
    /// Finds the person's records in every module and returns them.
    /// </summary>
    /// <param name="subject">The person the request is about.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Every record the modules keep about the person.</returns>
    Task<PersonalDataExport> ExportAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the person's records in every module, then removes or replaces the personal data in them.
    /// </summary>
    /// <param name="subject">The person the request is about.</param>
    /// <param name="replacementText">The text names are replaced with, or <see langword="null"/> to clear them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many records of each kind changed.</returns>
    Task<PersonalDataAnonymizeResult> AnonymizeAsync(PersonalDataSubject subject, string replacementText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymizes the person's records because a retention window ran out, such as the nightly expiry of inactive
    /// guests. Only the contributors whose <see cref="IPersonalDataContributor.TakesPartInRetentionExpiry"/> is
    /// <see langword="true"/> identify the person and anonymize their records; names are cleared.
    /// </summary>
    /// <param name="subject">The person the expiry is about.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many records of each kind changed.</returns>
    Task<PersonalDataAnonymizeResult> ExpireAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default);
}
