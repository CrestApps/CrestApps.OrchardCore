using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Pdf;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDocumentTextExtractor<PdfDocumentTextExtractor>(".pdf");
    }
}
