using CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Pdf;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDocumentTextExtractor<OpenXmlDocumentTextExtractor>(".docx", new ExtractorExtension(".xlsx", false), ".pptx");
    }
}
