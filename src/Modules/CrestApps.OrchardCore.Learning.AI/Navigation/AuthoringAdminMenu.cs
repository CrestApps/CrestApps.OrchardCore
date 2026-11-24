using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.AI.Navigation;

/// <summary>
/// Adds the authoring assistant to the Learning admin menu.
/// </summary>
public sealed class AuthoringAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthoringAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AuthoringAdminMenu(IStringLocalizer<AuthoringAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["Authoring Assistant"], S["Authoring Assistant"].PrefixPosition(), authoring => authoring
                .Id("learningAuthoringAssistant")
                .Action("Index", "Authoring", LearningAIConstants.Features.Assistant)
                .Permission(LearningAIPermissions.UseAuthoringAssistant)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
