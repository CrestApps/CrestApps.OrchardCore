using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Manages approval requests, so their handlers run.
/// </summary>
public sealed class ApprovalRequestManager : CatalogManager<ApprovalRequest>, IApprovalRequestManager
{
    private readonly IApprovalRequestStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalRequestManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public ApprovalRequestManager(IApprovalRequestStore store, IEnumerable<ICatalogEntryHandler<ApprovalRequest>> handlers, ILogger<CatalogManager<ApprovalRequest>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<ApprovalRequest> FindPendingAsync(string subjectKind, string subjectId, string policyId = null, CancellationToken cancellationToken = default)
        => _store.FindPendingAsync(subjectKind, subjectId, policyId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalRequest>> ListBySubjectAsync(string subjectKind, string subjectId, CancellationToken cancellationToken = default)
        => _store.ListBySubjectAsync(subjectKind, subjectId, cancellationToken);
}
