using CrestApps.OrchardCore.Pdf;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the PDF building block.
/// </summary>
public static class PdfServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPdfDocumentRenderer"/>, once however many modules call it.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPdfDocumentRenderer(this IServiceCollection services)
    {
        services.TryAddSingleton<IPdfDocumentRenderer, PdfDocumentRenderer>();

        return services;
    }
}
