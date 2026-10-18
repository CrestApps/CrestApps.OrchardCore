using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Adds the AI Profile Builder to the admin menu.
/// </summary>
internal sealed class ProfileBuilderAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBuilderAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProfileBuilderAdminMenu(IStringLocalizer<ProfileBuilderAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], artificialIntelligence =>
            {
                artificialIntelligence
                    .Add(S["Profile Builder"], S["Profile Builder"].PrefixPosition(), profileBuilder => profileBuilder
                        .Action("Index", "ProfileBuilder", ChatInteractionsConstants.Feature.ChatInteractions)
                        .Permission(AIPermissions.ManageAIProfiles)
                        .LocalNav()
                    );
            });

        return ValueTask.CompletedTask;
    }
}
