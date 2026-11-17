using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.SourceAccess;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// The requests waiting for a person's decision: the pending requests whose current stage names them and whose record
/// they may act on. It is the inbox of approvers who hold no Approvals permission, such as a manager named by a
/// policy.
/// </summary>
public sealed class ApprovalInbox
{
    /// <summary>
    /// How many pending requests are read at a time.
    /// </summary>
    public const int BatchSize = 500;

    private readonly IApprovalRequestStore _store;
    private readonly IApprovalService _approvals;
    private readonly ISourceAccessService _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalInbox"/> class.
    /// </summary>
    /// <param name="store">The request store.</param>
    /// <param name="approvals">The approval rules, for who may decide.</param>
    /// <param name="access">Who may act on the records of each type and scope.</param>
    public ApprovalInbox(IApprovalRequestStore store, IApprovalService approvals, ISourceAccessService access)
    {
        _store = store;
        _approvals = approvals;
        _access = access;
    }

    /// <summary>
    /// Lists a page of the requests waiting for a person's decision, oldest first.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="type">The approval type, or <see langword="null"/> for every type.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many are waiting, and the page.</returns>
    public async Task<(int Count, IReadOnlyList<ApprovalRequest> Requests)> ListWaitingAsync(ClaimsPrincipal user, string type, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Only requests about records the person may act on are read, in the database; each is then checked against
        // its current stage. Every pending request is looked at, a batch at a time, so the count is exact.
        var filter = await _access.GetFilterAsync(user, SourceAccessLevel.Manage, cancellationToken);
        var waiting = new List<ApprovalRequest>();

        for (var start = 0; ; start += BatchSize)
        {
            var (count, batch) = await _store.ListPageAsync(
                new ApprovalRequestQuery { Status = ApprovalStatus.Pending, Type = type, OldestFirst = true, Access = filter },
                start,
                BatchSize,
                cancellationToken);

            foreach (var request in batch)
            {
                if ((await _approvals.GetEligibilityAsync(request, user, cancellationToken)).CanDecide)
                {
                    waiting.Add(request);
                }
            }

            if (batch.Count < BatchSize || start + BatchSize >= count)
            {
                break;
            }
        }

        return (waiting.Count, [.. waiting.Skip(skip).Take(take)]);
    }
}
