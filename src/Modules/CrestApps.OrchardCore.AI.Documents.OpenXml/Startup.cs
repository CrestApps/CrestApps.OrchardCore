using CrestApps.Core.AI.Documents.OpenXml;
using CrestApps.Core.AI.Documents.Word;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.OpenXml;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Reads and writes .docx, .xlsx and .pptx, and brings the system presentation agent along with it.
        services.AddCoreAIOpenXmlDocumentProcessing();

        // The system Word agent writes, edits, reviews and previews .docx documents. It builds on the Open XML
        // .docx writer registered above, so it belongs to this feature rather than a feature of its own.
        services.AddCoreAIWordDocumentProcessing();
    }
}
