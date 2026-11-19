using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Accounts.Navigation;

/// <summary>
/// Adds Account members to the storefront account menu, for the members of an account: the page lists the members of
/// the signed-in user's account and is not found for anyone else.
/// </summary>
public sealed class AccountMembersAccountMenu : NamedNavigationProvider
{
    private readonly IAccountMemberService _accountMemberService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AccountMembersAccountMenu(
        IAccountMemberService accountMemberService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AccountMembersAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        _accountMemberService = accountMemberService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || await _accountMemberService.FindMembershipAsync(httpContext.User, httpContext.RequestAborted) is null)
        {
            return;
        }

        builder.Add(S["Account members"], "31", members => members
            .Id("account-members")
            .AddClass("fa-solid fa-users")
            .Url("~/account/members"));
    }
}
