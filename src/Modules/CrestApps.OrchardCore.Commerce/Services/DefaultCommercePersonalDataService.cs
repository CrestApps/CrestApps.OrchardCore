using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// Composes the <see cref="ICommercePersonalDataContributor"/> instances of the enabled commerce features into one
/// export or anonymization.
/// </summary>
public sealed class DefaultCommercePersonalDataService : ICommercePersonalDataService
{
    /// <summary>
    /// The most identification rounds a request runs. Each round can only follow one more link (an email to a
    /// checkout, a checkout to its transaction, a transaction to the checkout that paid it), so a few rounds reach
    /// everything while a misbehaving contributor still cannot loop forever.
    /// </summary>
    private const int MaxIdentificationRounds = 5;

    private readonly IEnumerable<ICommercePersonalDataContributor> _contributors;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCommercePersonalDataService"/> class.
    /// </summary>
    /// <param name="contributors">The contributors of the enabled commerce features.</param>
    /// <param name="clock">The clock the export is stamped with.</param>
    public DefaultCommercePersonalDataService(
        IEnumerable<ICommercePersonalDataContributor> contributors,
        IClock clock)
    {
        _contributors = contributors;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<CommercePersonalDataExport> ExportAsync(CustomerOwner owner, string email, CancellationToken cancellationToken = default)
    {
        var subject = await IdentifyAsync(_contributors, owner, email, cancellationToken);

        var export = new CommercePersonalDataExport
        {
            Owner = owner,
            Email = subject.Email,
            ExportedUtc = _clock.UtcNow,
        };

        var context = new CommercePersonalDataExportContext(subject, export);

        foreach (var contributor in _contributors)
        {
            await contributor.ExportAsync(context, cancellationToken);
        }

        return export;
    }

    /// <inheritdoc/>
    public Task<CommercePersonalDataAnonymizeResult> AnonymizeAsync(CustomerOwner owner, string email, string replacementText, CancellationToken cancellationToken = default)
        => AnonymizeAsync(_contributors, owner, email, replacementText, cancellationToken);

    /// <inheritdoc/>
    public Task<CommercePersonalDataAnonymizeResult> ExpireAsync(CustomerOwner owner, string email, CancellationToken cancellationToken = default)
    {
        // The guest expiry reaches only the commerce records it always reached; the contributors that opted out are
        // left to an explicit erasure request.
        var contributors = _contributors.Where(contributor => contributor.TakesPartInRetentionExpiry).ToArray();

        return AnonymizeAsync(contributors, owner, email, replacementText: null, cancellationToken);
    }

    private static async Task<CommercePersonalDataAnonymizeResult> AnonymizeAsync(
        IEnumerable<ICommercePersonalDataContributor> contributors,
        CustomerOwner owner,
        string email,
        string replacementText,
        CancellationToken cancellationToken)
    {
        // Everything is identified before anything changes: a contributor that anonymized first would remove the very
        // email another contributor finds its records by.
        var subject = await IdentifyAsync(contributors, owner, email, cancellationToken);

        var result = new CommercePersonalDataAnonymizeResult();
        var context = new CommercePersonalDataAnonymizeContext(subject, replacementText, result);

        foreach (var contributor in contributors)
        {
            await contributor.AnonymizeAsync(context, cancellationToken);
        }

        return result;
    }

    private static async Task<CommercePersonalDataSubject> IdentifyAsync(
        IEnumerable<ICommercePersonalDataContributor> contributors,
        CustomerOwner owner,
        string email,
        CancellationToken cancellationToken)
    {
        var subject = new CommercePersonalDataSubject(owner, email);

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

        return subject;
    }
}
