using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Certifications.Navigation;

/// <summary>
/// Adds My competencies to the storefront account menu, beside My certifications.
/// </summary>
internal sealed class CompetenciesAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public CompetenciesAccountMenu(IStringLocalizer<CompetenciesAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["My competencies"], "30.7", competencies => competencies
                .Id("account-learning-competencies")
                .AddClass("fa-solid fa-medal")
                .Url("~/learning/competencies")
                .Permission(LearningPermissions.AccessLearning));

        return ValueTask.CompletedTask;
    }
}
