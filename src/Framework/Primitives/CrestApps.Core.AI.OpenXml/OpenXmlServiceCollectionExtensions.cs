using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenXml.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.AI.OpenXml;

public static class OpenXmlServiceCollectionExtensions
{
    public static IServiceCollection AddOpenXmlDocumentProcessingServices(this IServiceCollection services)
    {
        services.AddIngestionDocumentReader<OpenXmlIngestionDocumentReader>(
            ".docx",
            new ExtractorExtension(".xlsx", false),
            ".pptx");

        return services;
    }
}
