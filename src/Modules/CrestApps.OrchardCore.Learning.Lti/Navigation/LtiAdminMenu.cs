using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Lti.Navigation;

/// <summary>
/// Adds External tools to the Learning menu.
/// </summary>
internal sealed class LtiAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public LtiAdminMenu(IStringLocalizer<LtiAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["External Tools"], S["External Tools"].PrefixPosition(), tools => tools
                .AddClass("learning-external-tools")
                .Id("learningExternalTools")
                .Action("Index", "ExternalTools", LtiConstants.Features.ExternalTools)
                .Permission(LearningLtiPermissions.ManageExternalTools)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
