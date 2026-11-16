using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// The requests waiting for a person's decision: the pending requests whose current stage names them, whatever they
/// may see otherwise. It is the inbox of approvers who hold no Approvals permission, such as a manager named by a
/// policy.
/// </summary>
public sealed class ApprovalInbox
{
    /// <summary>
    /// The most pending requests looked at for one inbox, oldest first.
    /// </summary>
    public const int MaxPendingScanned = 1000;

    private readonly IApprovalRequestStore _store;
    private readonly IApprovalService _approvals;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalInbox"/> class.
    /// </summary>
    /// <param name="store">The request store.</param>
    /// <param name="approvals">The approval rules, for who may decide.</param>
    public ApprovalInbox(IApprovalRequestStore store, IApprovalService approvals)
    {
        _store = store;
        _approvals = approvals;
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

        var (_, pending) = await _store.ListPageAsync(
            new ApprovalRequestQuery { Status = ApprovalStatus.Pending, Type = type, OldestFirst = true },
            0,
            MaxPendingScanned,
            cancellationToken);

        var waiting = new List<ApprovalRequest>();

        foreach (var request in pending)
        {
            if ((await _approvals.GetEligibilityAsync(request, user, cancellationToken)).CanDecide)
            {
                waiting.Add(request);
            }
        }

        return (waiting.Count, [.. waiting.Skip(skip).Take(take)]);
    }
}
