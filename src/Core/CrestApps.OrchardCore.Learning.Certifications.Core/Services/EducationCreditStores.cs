using CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// The education credit ledger, a quantity ledger of its own collection: the account is the user, the bucket the
/// credit type.
/// </summary>
public sealed class EducationCreditLedger
{
}

/// <summary>
/// Stores education credit types.
/// </summary>
public sealed class EducationCreditTypeStore : DocumentCatalog<EducationCreditType, EducationCreditTypeIndex>
{
    public EducationCreditTypeStore(ISession session)
        : base(session)
    {
        CollectionName = CertificationsConstants.CollectionName;
    }

    /// <summary>
    /// Lists every credit type by name.
    /// </summary>
    public async Task<IReadOnlyList<EducationCreditType>> ListAllAsync(CancellationToken cancellationToken = default)
        => (await Session.Query<EducationCreditType, EducationCreditTypeIndex>(collection: CollectionName).OrderBy(index => index.Name).ListAsync(cancellationToken)).ToArray();
}

/// <summary>
/// Stores external learning.
/// </summary>
public sealed class ExternalLearningSubmissionStore : DocumentCatalog<ExternalLearningSubmission, ExternalLearningSubmissionIndex>
{
    public ExternalLearningSubmissionStore(ISession session)
        : base(session)
    {
        CollectionName = CertificationsConstants.CollectionName;
    }

    /// <summary>
    /// Lists the external learning of a person, newest first.
    /// </summary>
    public async Task<IReadOnlyList<ExternalLearningSubmission>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => (await Session.Query<ExternalLearningSubmission, ExternalLearningSubmissionIndex>(index => index.UserId == userId, collection: CollectionName)
            .OrderByDescending(index => index.SubmittedUtc)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Pages external learning, newest first.
    /// </summary>
    public async Task<(int Count, IReadOnlyList<ExternalLearningSubmission> Items)> PageAsync(ExternalLearningState? state, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<ExternalLearningSubmission, ExternalLearningSubmissionIndex>(collection: CollectionName);

        if (state is { } value)
        {
            query = query.Where(index => index.State == value);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.SubmittedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }
}
