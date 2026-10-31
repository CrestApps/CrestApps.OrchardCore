using CrestApps.Core.AI.Cartesia;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Cartesia.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Cartesia;

/// <summary>
/// Registers the Cartesia speech provider: text-to-speech, speech recognition and its voice library.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAICartesia();

        services.AddDisplayDriver<AIDeployment, CartesiaDeploymentDisplayDriver>();
    }
}
