using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Adds Course seats to the storefront account menu.
/// </summary>
internal sealed class LearningSeatsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public LearningSeatsAccountMenu(IStringLocalizer<LearningSeatsAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Course seats"], "32", seats => seats
            .Id("account-learning-seats")
            .AddClass("fa-solid fa-chair")
            .Url("~/account/learning-seats"));

        return ValueTask.CompletedTask;
    }
}
