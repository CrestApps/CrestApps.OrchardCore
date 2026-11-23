using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores surveys, one document each.
/// </summary>
public sealed class SurveyStore : DocumentCatalog<Survey, SurveyIndex>, INamedCatalog<Survey>
{
    private readonly SurveyContentSanitizer _sanitizer;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="sanitizer">Sanitizes the draft's HTML texts on every save; the module registers it.</param>
    /// <param name="dataProtectionProvider">Protects the anonymity secret on every save, so no save stores it in plain
    /// text (see <see cref="SurveyAnonymitySecrets"/>); the host registers it.</param>
    public SurveyStore(ISession session, SurveyContentSanitizer sanitizer = null, IDataProtectionProvider dataProtectionProvider = null)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
        _sanitizer = sanitizer;
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <inheritdoc />
    protected override ValueTask SavingAsync(Survey record)
    {
        _sanitizer?.Sanitize(record?.Draft);

        if (record is not null && _dataProtectionProvider is not null)
        {
            // Every survey write goes through here: a survey stored before secrets were protected is protected by any
            // save, not only by the upgrade step, which may not reach a survey saved while it walks.
            _protector ??= SurveyAnonymitySecretProtection.CreateProtector(_dataProtectionProvider);
            SurveyAnonymitySecretProtection.EnsureProtected(_protector, record);
        }

        return base.SavingAsync(record);
    }

    /// <summary>
    /// Saves a survey again as loaded, with the checks of every save, and with YesSql's concurrency check: when another
    /// save changed the survey after it was loaded, the commit fails with a <see cref="ConcurrencyException"/> and writes
    /// nothing, so a background rewrite (such as a reindex) never overwrites an author's edit.
    /// </summary>
    /// <param name="record">The survey, loaded in this session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async ValueTask SaveUnchangedAsync(Survey record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await SavingAsync(record);
        await Session.SaveAsync(record, checkConcurrency: true, collection: CollectionName, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Survey> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        return await Session.Query<Survey, SurveyIndex>(index => index.Name == trimmed, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the surveys a record owns.
    /// </summary>
    /// <param name="ownerRef">The owner as Kind:Id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The surveys.</returns>
    public async Task<IReadOnlyList<Survey>> ListByOwnerAsync(string ownerRef, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ownerRef))
        {
            return [];
        }

        var items = await Session.Query<Survey, SurveyIndex>(index => index.OwnerRef == ownerRef, collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Lists the surveys any of several records own. The owners are read a chunk at a time (<see cref="SurveyIdChunks"/>),
    /// so the <c>IN</c> list stays under SQL Server's limit of 2,100 parameters.
    /// </summary>
    /// <param name="ownerRefs">The owners as Kind:Id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The surveys.</returns>
    public async Task<IReadOnlyList<Survey>> ListByOwnersAsync(IReadOnlyCollection<string> ownerRefs, CancellationToken cancellationToken = default)
    {
        var surveys = new List<Survey>();

        foreach (var chunk in SurveyIdChunks.Of(ownerRefs))
        {
            surveys.AddRange(await Session.Query<Survey, SurveyIndex>(index => index.OwnerRef.IsIn(chunk), collection: SurveysConstants.CollectionName)
                .ListAsync(cancellationToken));
        }

        return surveys;
    }
}
