using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Providers;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.Contents;

/// <summary>
/// Registers report field providers for content fields.
/// </summary>
public static class ContentReportServiceCollectionExtensions
{
    /// <summary>
    /// Registers a provider for a content field type whose report value is one JSON property of the field. The
    /// field contributes one report field named <c>{PartName}.{FieldName}</c>. A provider registered later for the
    /// same field type replaces this one.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="fieldType">The technical name of the content field type, such as <c>TextField</c>.</param>
    /// <param name="dataType">The data type of the report field.</param>
    /// <param name="mode">How the property becomes a value.</param>
    /// <param name="propertyNames">The JSON property names to try, in order.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddContentReportFieldProvider(
        this IServiceCollection services,
        string fieldType,
        ReportDataType dataType,
        ContentReportValueMode mode,
        params string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IContentReportFieldProvider>(new PropertyContentReportFieldProvider(fieldType, dataType, mode, propertyNames));

        return services;
    }
}
