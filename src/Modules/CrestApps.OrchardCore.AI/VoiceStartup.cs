using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the profile's voice and speech settings and the voice library page.
/// </summary>
public sealed class VoiceStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDisplayDriver<AIProfile, AIProfileSpeechDisplayDriver>()
            .AddDisplayDriver<SpeechVoice, SpeechVoiceDisplayDriver>()
            .AddNavigationProvider<AIVoiceLibraryAdminMenu>();
    }
}

/// <summary>
/// Adds the voice library to the Artificial Intelligence menu.
/// </summary>
public sealed class AIVoiceLibraryAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceLibraryAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIVoiceLibraryAdminMenu(IStringLocalizer<AIVoiceLibraryAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Voice library"], S["Voice library"].PrefixPosition(), voices => voices
                    .Id("aiVoiceLibrary")
                    .Action("Index", "AIVoiceLibrary", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIDeployments)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
