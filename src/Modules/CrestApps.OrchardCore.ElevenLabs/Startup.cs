using CrestApps.Core.AI.ElevenLabs;
using Microsoft.Extensions.DependencyInjection;
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
    }
}
