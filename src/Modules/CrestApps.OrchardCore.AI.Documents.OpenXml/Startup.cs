using CrestApps.Core.AI.Documents.OpenXml;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.OpenXml;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIOpenXmlDocumentProcessing();
    }
}
