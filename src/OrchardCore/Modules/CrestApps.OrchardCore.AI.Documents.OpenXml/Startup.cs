using CrestApps.AI;
using CrestApps.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.OpenXml;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIngestionDocumentReader<OpenXmlIngestionDocumentReader>(".docx", new ExtractorExtension(".xlsx", false), ".pptx");
    }
}
