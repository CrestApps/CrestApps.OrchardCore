using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Navigation;

/// <summary>
/// Adds My learning and My transcript to the storefront account menu.
/// </summary>
internal sealed class LearningAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public LearningAccountMenu(IStringLocalizer<LearningAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["My learning"], "30", learning => learning
                .Id("account-learning")
                .AddClass("fa-solid fa-graduation-cap")
                .Url("~/learning")
                .Permission(LearningPermissions.AccessLearning))
            .Add(S["My transcript"], "30.4", transcript => transcript
                .Id("account-learning-transcript")
                .AddClass("fa-solid fa-scroll")
                .Url("~/learning/transcript")
                .Permission(LearningPermissions.AccessLearning));

        return ValueTask.CompletedTask;
    }
}
