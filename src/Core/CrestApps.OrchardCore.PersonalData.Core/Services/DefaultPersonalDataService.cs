using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PersonalData.Core.Services;

/// <summary>
/// Identifies the person through every contributor until nothing new is found, then exports or anonymizes their
/// records through each contributor in order.
/// </summary>
public sealed class DefaultPersonalDataService : IPersonalDataService
{
    private const int MaxIdentificationRounds = 5;

    private readonly IPersonalDataContributor[] _contributors;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPersonalDataService"/> class.
    /// </summary>
    /// <param name="contributors">The registered contributors.</param>
    /// <param name="clock">The clock.</param>
    public DefaultPersonalDataService(
        IEnumerable<IPersonalDataContributor> contributors,
        IClock clock)
    {
        // A stable sort, so contributors with the same order keep their registration order.
        _contributors = [.. contributors.OrderBy(contributor => contributor.Order)];
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<PersonalDataExport> ExportAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        await IdentifyAsync(_contributors, subject, cancellationToken);

        var export = new PersonalDataExport
        {
            ExportedUtc = _clock.UtcNow,
            UserIds = [.. subject.UserIds],
            Emails = [.. subject.Emails],
            PhoneNumbers = [.. subject.PhoneNumbers],
        };

        var context = new PersonalDataExportContext(subject, export);

        foreach (var contributor in _contributors)
        {
            await contributor.ExportAsync(context, cancellationToken);
        }

        return export;
    }

    /// <inheritdoc/>
    public Task<PersonalDataAnonymizeResult> AnonymizeAsync(PersonalDataSubject subject, string replacementText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return AnonymizeAsync(_contributors, subject, replacementText, PersonalDataRequestKind.Erasure, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PersonalDataAnonymizeResult> ExpireAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // A retention window only reaches the contributors that opted in, such as commerce: the CRM, Workforce,
        // Scheduling and the other modules' records are only ever erased by an explicit request.
        var contributors = _contributors.Where(contributor => contributor.TakesPartInRetentionExpiry).ToArray();

        return AnonymizeAsync(contributors, subject, replacementText: null, PersonalDataRequestKind.RetentionExpiry, cancellationToken);
    }

    private static async Task<PersonalDataAnonymizeResult> AnonymizeAsync(
        IPersonalDataContributor[] contributors,
        PersonalDataSubject subject,
        string replacementText,
        PersonalDataRequestKind kind,
        CancellationToken cancellationToken)
    {
        // Everything is identified before anything changes: a contributor that anonymized first would remove the very
        // email another contributor finds its records by.
        await IdentifyAsync(contributors, subject, cancellationToken);

        var result = new PersonalDataAnonymizeResult();
        var context = new PersonalDataAnonymizeContext(subject, replacementText, result, kind);

        foreach (var contributor in contributors)
        {
            await contributor.AnonymizeAsync(context, cancellationToken);
        }

        return result;
    }

    private static async Task IdentifyAsync(IPersonalDataContributor[] contributors, PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        for (var round = 0; round < MaxIdentificationRounds; round++)
        {
            var version = subject.Version;

            foreach (var contributor in contributors)
            {
                await contributor.IdentifyAsync(subject, cancellationToken);
            }

            if (subject.Version == version)
            {
                break;
            }
        }
    }
}
