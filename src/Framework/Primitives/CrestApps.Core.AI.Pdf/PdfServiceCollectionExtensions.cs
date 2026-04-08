using CrestApps.Core.AI.Pdf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.AI.Pdf;

public static class PdfServiceCollectionExtensions
{
    public static IServiceCollection AddPdfDocumentProcessingServices(this IServiceCollection services)
    {
        services.AddIngestionDocumentReader<PdfIngestionDocumentReader>(".pdf");

        return services;
    }
}
