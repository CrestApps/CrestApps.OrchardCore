using CrestApps.AI.Pdf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.AI.Pdf;

public static class PdfServiceCollectionExtensions
{
    public static IServiceCollection AddPdfDocumentProcessingServices(this IServiceCollection services)
    {
        services.AddIngestionDocumentReader<PdfIngestionDocumentReader>(".pdf");

        return services;
    }
}
