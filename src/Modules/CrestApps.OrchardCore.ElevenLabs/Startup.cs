using CrestApps.Core.AI.ElevenLabs;
using CrestApps.Core.AI.Models;
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

        services.AddDisplayDriver<AIDeployment, ElevenLabsDeploymentDisplayDriver>();
    }
}
