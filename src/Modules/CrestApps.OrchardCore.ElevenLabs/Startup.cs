using CrestApps.Core.AI.ElevenLabs;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.ElevenLabs.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ElevenLabs;

/// <summary>
/// Registers the ElevenLabs speech provider: text-to-speech, speech recognition and its voice library.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIElevenLabs();

        // The speech client calls the deployment's endpoint, which an administrator may type; it refuses the site's own
        // network at connect.
        services.AddAIEgressGuards();

        services.AddDisplayDriver<AIDeployment, ElevenLabsDeploymentDisplayDriver>();
    }
}
