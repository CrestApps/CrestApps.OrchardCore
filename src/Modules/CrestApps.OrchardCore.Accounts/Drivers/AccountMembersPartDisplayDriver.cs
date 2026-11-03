using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Accounts.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Accounts.Drivers;

/// <summary>
/// The members on an account's editor: the users who sign in for the account, by user name or email, and their role.
/// A user is a member of one account at most. Only users who manage account members see them or change them.
/// </summary>
internal sealed class AccountMembersPartDisplayDriver : ContentPartDisplayDriver<AccountMembersPart>
{
    // Empty rows the editor offers to add members.
    private const int SpareRows = 2;

    private readonly UserManager<IUser> _userManager;
    private readonly IAccountMemberService _accountMemberService;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AccountMembersPartDisplayDriver(
        UserManager<IUser> userManager,
        IAccountMemberService accountMemberService,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AccountMembersPartDisplayDriver> stringLocalizer)
    {
        _userManager = userManager;
        _accountMemberService = accountMemberService;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AccountMembersPart part, BuildPartEditorContext context)
    {
        if (!await CanManageAsync())
        {
            return null;
        }

        var rows = (part.Members ?? [])
            .Where(member => member is not null)
            .Select(member => new AccountMemberRowViewModel
            {
                User = member.Email ?? member.UserName,
                Role = member.Role,
                ContactContentItemId = member.ContactContentItemId,
            })
            .ToList();

        return BuildEditor(rows, context);
    }

    public override async Task<IDisplayResult> UpdateAsync(AccountMembersPart part, UpdatePartEditorContext context)
    {
        if (!await CanManageAsync())
        {
            return null;
        }

        var model = new AccountMembersPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var accountId = part.ContentItem.ContentItemId;
        var members = new List<AccountMember>();
        var valid = true;

        foreach (var row in (model.Members ?? []).Where(row => !string.IsNullOrWhiteSpace(row?.User)))
        {
            var name = row.User.Trim();
            var user = await _userManager.FindByEmailAsync(name) ?? await _userManager.FindByNameAsync(name);

            if (user is null)
            {
                context.Updater.ModelState.AddModelError(Key(), S["There's no user {0}. Add them as a user first.", name]);
                valid = false;

                continue;
            }

            var userId = await _userManager.GetUserIdAsync(user);

            if (members.Any(member => member.UserId == userId))
            {
                continue;
            }

            if (await _accountMemberService.FindOtherAccountIdAsync(userId, accountId) is { } otherId)
            {
                var other = await _contentManager.GetAsync(otherId, VersionOptions.Latest);

                context.Updater.ModelState.AddModelError(Key(), S["{0} is already a member of {1}.", name, other?.DisplayText ?? otherId]);
                valid = false;

                continue;
            }

            members.Add(new AccountMember
            {
                UserId = userId,
                UserName = await _userManager.GetUserNameAsync(user),
                Email = await _userManager.GetEmailAsync(user),
                Role = row.Role,
                ContactContentItemId = part.FindMember(userId)?.ContactContentItemId,
            });
        }

        if (!valid)
        {
            return BuildEditor(model.Members ?? [], context);
        }

        part.Members = members;

        return await EditAsync(part, context);
    }

    private ShapeResult BuildEditor(List<AccountMemberRowViewModel> rows, BuildPartEditorContext context)
        => Initialize<AccountMembersPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Members = [.. rows.Where(row => !string.IsNullOrWhiteSpace(row?.User))];

            for (var i = 0; i < SpareRows; i++)
            {
                model.Members.Add(new AccountMemberRowViewModel());
            }
        });

    private async Task<bool> CanManageAsync()
        => _httpContextAccessor.HttpContext?.User is { } user &&
        await _authorizationService.AuthorizeAsync(user, AccountsPermissions.ManageAccountMembers);

    private string Key()
        => $"{Prefix}.{nameof(AccountMembersPartViewModel.Members)}";
}
