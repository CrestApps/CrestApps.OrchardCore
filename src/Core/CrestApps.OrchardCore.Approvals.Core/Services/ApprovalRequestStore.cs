using CrestApps.OrchardCore.Approvals.Core.Indexes;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.YesSql.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.SourceAccess;
using YesSql;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Stores approval requests in YesSql.
/// </summary>
public sealed class ApprovalRequestStore : DocumentCatalog<ApprovalRequest, ApprovalRequestIndex>, IApprovalRequestStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalRequestStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ApprovalRequestStore(ISession session)
        : base(session)
    {
        CollectionName = ApprovalsConstants.CollectionName;
    }

    /// <inheritdoc />
    public Task<ApprovalRequest> FindPendingAsync(string subjectKind, string subjectId, string policyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectKind);
        ArgumentException.ThrowIfNullOrEmpty(subjectId);

        var pending = nameof(ApprovalStatus.Pending);
        var query = Session.Query<ApprovalRequest, ApprovalRequestIndex>(index => index.SourceKind == subjectKind && index.SubjectId == subjectId && index.Status == pending, collection: CollectionName);

        if (!string.IsNullOrEmpty(policyId))
        {
            query = query.Where(index => index.PolicyId == policyId);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalRequest>> ListBySubjectAsync(string subjectKind, string subjectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectKind);
        ArgumentException.ThrowIfNullOrEmpty(subjectId);

        var requests = await Session.Query<ApprovalRequest, ApprovalRequestIndex>(index => index.SourceKind == subjectKind && index.SubjectId == subjectId, collection: CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return requests.ToList();
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<ApprovalRequest> Requests)> ListPageAsync(ApprovalRequestQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requests = Session.Query<ApprovalRequest, ApprovalRequestIndex>(collection: CollectionName);

        if (SourceAccessExpressions.Restrict<ApprovalRequestIndex>(query.Access ?? SourceAccessFilter.Unrestricted) is { } restrict)
        {
            requests = requests.Where(restrict);
        }

        if (query.Status is { } status)
        {
            var name = status.ToString();
            requests = requests.Where(index => index.Status == name);
        }

        if (!string.IsNullOrEmpty(query.PolicyId))
        {
            var policyId = query.PolicyId;
            requests = requests.Where(index => index.PolicyId == policyId);
        }

        if (!string.IsNullOrEmpty(query.Type))
        {
            var type = query.Type;
            requests = requests.Where(index => index.SourceKind == type);
        }

        if (!string.IsNullOrEmpty(query.RequesterUserId))
        {
            var requester = query.RequesterUserId;
            requests = requests.Where(index => index.RequesterUserId == requester);
        }

        var count = await requests.CountAsync(cancellationToken);
        var ordered = query.OldestFirst
            ? requests.OrderBy(index => index.CreatedUtc).ThenBy(index => index.Id)
            : requests.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id);
        var page = await ordered.Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToList());
    }
}
