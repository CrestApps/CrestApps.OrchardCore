using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Accounts.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Accounts.Controllers;

/// <summary>
/// Accepting an invitation to an account, and the members page an account admin invites people from.
/// </summary>
[Feature(AccountsConstants.MembersFeature)]
public sealed class AccountInvitationsController : Controller
{
    private readonly IAccountInvitationService _invitations;
    private readonly IAccountMemberService _members;
    private readonly IContentManager _contentManager;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly UserManager<IUser> _userManager;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public AccountInvitationsController(
        IAccountInvitationService invitations,
        IAccountMemberService members,
        IContentManager contentManager,
        INotifier notifier,
        IClock clock,
        UserManager<IUser> userManager,
        IHtmlLocalizer<AccountInvitationsController> htmlLocalizer,
        IStringLocalizer<AccountInvitationsController> stringLocalizer)
    {
        _invitations = invitations;
        _members = members;
        _contentManager = contentManager;
        _notifier = notifier;
        _clock = clock;
        _userManager = userManager;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows an invitation: who invited the person to which account, and the button that accepts it.
    /// </summary>
    [HttpGet("account/invitations/{token}", Name = "AccountInvitation")]
    public async Task<IActionResult> Show(string token)
    {
        // The token is in the path: never hand it to another site in a Referer header.
        Response.Headers["Referrer-Policy"] = "no-referrer";

        var invitation = await _invitations.FindByTokenAsync(token, HttpContext.RequestAborted);
        var account = invitation is null ? null : await _contentManager.GetAsync(invitation.AccountId, VersionOptions.Published);

        // Accepting needs a confirmed address, so a signed-in person without one is offered the confirmation e-mail.
        var user = User.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;

        return View(new AccountInvitationViewModel
        {
            NeedsEmailConfirmation = user is not null && !await _userManager.IsEmailConfirmedAsync(user),
            Token = token,
            Invitation = invitation,
            AccountName = account?.DisplayText,
            IsValid = invitation is { State: AccountInvitationState.Pending } && account is not null && invitation.ExpiresUtc >= _clock.UtcNow,
            IsExpired = invitation is not null && (invitation.State == AccountInvitationState.Expired || (invitation.State == AccountInvitationState.Pending && invitation.ExpiresUtc < _clock.UtcNow)),
        });
    }

    /// <summary>
    /// Accepts an invitation for the signed-in user.
    /// </summary>
    [HttpPost("account/invitations/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(string token)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var result = await _invitations.AcceptAsync(token, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", result.Error ?? string.Empty]);

            return RedirectToRoute("AccountInvitation", new { token });
        }

        await _notifier.SuccessAsync(H["Welcome: you are now a member of the account."]);

        return RedirectToAction(nameof(Members));
    }

    /// <summary>
    /// Lists the members of the signed-in user's account and its invitations; an account admin invites from here.
    /// </summary>
    [Authorize]
    [HttpGet("account/members", Name = "AccountMembers")]
    public async Task<IActionResult> Members()
    {
        var model = await BuildAsync();

        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Invites a person to the account.
    /// </summary>
    [Authorize]
    [HttpPost("account/members/invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(string email, AccountMemberRole role, string message)
    {
        var (account, isAdmin) = await FindAccountAsync();

        if (account is null || !isAdmin)
        {
            return Forbid();
        }

        var result = await _invitations.InviteAsync(new AccountInvitationRequest
        {
            AccountId = account.ContentItemId,
            Email = email,
            Role = role,
            Message = message,
            InvitedBy = User.Identity?.Name,
        }, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["{0} is invited. The invitation link works for 14 days.", result.Invitation.Email]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Error ?? string.Empty]);
        }

        return RedirectToAction(nameof(Members));
    }

    /// <summary>
    /// Takes an invitation back.
    /// </summary>
    [Authorize]
    [HttpPost("account/members/invitations/{id}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(string id)
    {
        var (account, isAdmin) = await FindAccountAsync();
        var invitation = await _invitations.FindByIdAsync(id, HttpContext.RequestAborted);

        if (account is null || !isAdmin || invitation?.AccountId != account.ContentItemId)
        {
            return Forbid();
        }

        if (await _invitations.RevokeAsync(id, User.Identity?.Name, HttpContext.RequestAborted) is null)
        {
            await _notifier.WarningAsync(H["The invitation to {0} was already accepted, taken back or expired.", invitation.Email]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The invitation to {0} has been taken back.", invitation.Email]);
        }

        return RedirectToAction(nameof(Members));
    }

    private async Task<AccountMembersViewModel> BuildAsync()
    {
        var (account, isAdmin) = await FindAccountAsync();

        if (account is null)
        {
            return null;
        }

        return new AccountMembersViewModel
        {
            AccountName = account.DisplayText,
            IsAdmin = isAdmin,
            Members = account.TryGet<AccountMembersPart>(out var part) ? part.Members ?? [] : [],
            Invitations = isAdmin
                ? (await _invitations.ListForAccountAsync(account.ContentItemId, HttpContext.RequestAborted)).Where(invitation => invitation.State == AccountInvitationState.Pending).ToArray()
                : [],
        };
    }

    private async Task<(ContentItem Account, bool IsAdmin)> FindAccountAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var account = string.IsNullOrEmpty(userId) ? null : await _members.FindAccountAsync(userId, HttpContext.RequestAborted);
        var isAdmin = account is not null && account.TryGet<AccountMembersPart>(out var part) && part.FindMember(userId)?.Role == AccountMemberRole.Admin;

        return (account, isAdmin);
    }
}
