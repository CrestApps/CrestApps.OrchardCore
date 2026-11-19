using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Adds Course seats to the storefront account menu, for the admins of an account: the page refuses everyone else.
/// </summary>
internal sealed class LearningSeatsAccountMenu : NamedNavigationProvider
{
    private readonly IAccountMemberService _accountMemberService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public LearningSeatsAccountMenu(
        IAccountMemberService accountMemberService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<LearningSeatsAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        _accountMemberService = accountMemberService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || await _accountMemberService.FindMembershipAsync(httpContext.User, httpContext.RequestAborted) is not { IsAdmin: true })
        {
            return;
        }

        builder.Add(S["Course seats"], "32", seats => seats
            .Id("account-learning-seats")
            .AddClass("fa-solid fa-chair")
            .Url("~/account/learning-seats"));
    }
}
