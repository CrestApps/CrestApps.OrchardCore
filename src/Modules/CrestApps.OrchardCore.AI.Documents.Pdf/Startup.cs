using CrestApps.Core.AI.Documents.Pdf;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Pdf;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIPdfDocumentProcessing();
    }
}
