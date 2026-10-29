using CrestApps.Core.AI.Cartesia;
using Microsoft.Extensions.DependencyInjection;
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
    }
}
