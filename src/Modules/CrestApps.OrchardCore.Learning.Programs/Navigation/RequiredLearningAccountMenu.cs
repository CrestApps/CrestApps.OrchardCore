using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Programs.Navigation;

/// <summary>
/// Adds My required learning, the learner's required learning and learning paths, to the storefront account menu.
/// </summary>
internal sealed class RequiredLearningAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public RequiredLearningAccountMenu(IStringLocalizer<RequiredLearningAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["My required learning"], "30.2", required => required
            .Id("account-learning-required")
            .AddClass("fa-solid fa-list-check")
            .Url("~/learning/required")
            .Permission(LearningPermissions.AccessLearning));

        return ValueTask.CompletedTask;
    }
}
