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

        await IdentifyAsync(subject, cancellationToken);

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
    public async Task<PersonalDataAnonymizeResult> AnonymizeAsync(PersonalDataSubject subject, string replacementText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // Everything is identified before anything changes: a contributor that anonymized first would remove the very
        // email another contributor finds its records by.
        await IdentifyAsync(subject, cancellationToken);

        var result = new PersonalDataAnonymizeResult();
        var context = new PersonalDataAnonymizeContext(subject, replacementText, result);

        foreach (var contributor in _contributors)
        {
            await contributor.AnonymizeAsync(context, cancellationToken);
        }

        return result;
    }

    private async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        for (var round = 0; round < MaxIdentificationRounds; round++)
        {
            var version = subject.Version;

            foreach (var contributor in _contributors)
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
