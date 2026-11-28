using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Who may open and withdraw an approval request: the rules of Approvals › Requests, shared by its pages and the AI
/// tools. A request opens for those who may decide it, for its requester and those who decided it, and for those who
/// see it through the View approval requests permission and the subject type's access; anyone else gets nothing, not
/// even that it exists.
/// </summary>
internal sealed class ApprovalRequestAccess
{
    private readonly IApprovalService _approvalService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISourceAccessService _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalRequestAccess"/> class.
    /// </summary>
    /// <param name="approvalService">The approval rules, for who may decide.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Who may act on the records of each type and scope.</param>
    public ApprovalRequestAccess(IApprovalService approvalService, IAuthorizationService authorizationService, ISourceAccessService access)
    {
        _approvalService = approvalService;
        _authorizationService = authorizationService;
        _access = access;
    }

    /// <summary>
    /// Returns whether a person may open a request: <see cref="SourceAccessLevel.View"/> to read it,
    /// <see cref="SourceAccessLevel.Manage"/> to decide or withdraw it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="user">The person.</param>
    /// <param name="level">What the person does with it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the request opens for them.</returns>
    public async Task<bool> CanSeeAsync(ApprovalRequest request, ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        // Its requester, and whoever decided a stage of it, keep seeing it.
        if (userId is not null && (request.RequesterUserId == userId || request.Decisions.Any(decision => decision.UserId == userId)))
        {
            return true;
        }

        if ((await _approvalService.GetEligibilityAsync(request, user, cancellationToken)).CanDecide)
        {
            return true;
        }

        return await _authorizationService.AuthorizeAsync(user, ApprovalsPermissions.ViewApprovalRequests)
            && await _access.CanAccessAsync(user, request.SubjectKind, request.AccessScope, level, cancellationToken);
    }

    /// <summary>
    /// Returns whether a person may withdraw a pending request: its requester, or someone who manages requests and may
    /// act on its record.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    public async Task<bool> CanWithdrawAsync(ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Status == ApprovalStatus.Pending
            && ((request.RequesterUserId is { } requester && requester == user?.FindFirstValue(ClaimTypes.NameIdentifier))
                || (await _authorizationService.AuthorizeAsync(user, ApprovalsPermissions.ManageApprovalRequests)
                    && await _access.CanAccessAsync(user, request.SubjectKind, request.AccessScope, SourceAccessLevel.Manage, cancellationToken)));
    }
}
