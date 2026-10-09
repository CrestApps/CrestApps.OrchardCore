using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Providers;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Contents;

/// <summary>
/// Registers the content type report data source and the report field providers of the standard content fields and
/// parts. It needs no feature of its own: the content items become a data source as soon as the Report Designer and
/// Orchard Core Contents are both enabled.
/// </summary>
[Feature(ReportsConstants.DesignerFeature)]
[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentsReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ContentReportSchemaBuilder>();
        services.AddScoped<IReportDataSource, ContentsReportDataSource>();

        services
            .AddContentReportFieldProvider("TextField", ReportDataType.Text, ContentReportValueMode.Single, "Text")
            .AddContentReportFieldProvider("NumericField", ReportDataType.Decimal, ContentReportValueMode.Single, "Value")
            .AddContentReportFieldProvider("BooleanField", ReportDataType.Boolean, ContentReportValueMode.Single, "Value")
            .AddContentReportFieldProvider("DateField", ReportDataType.Date, ContentReportValueMode.Single, "Value")
            .AddContentReportFieldProvider("DateTimeField", ReportDataType.DateTime, ContentReportValueMode.Single, "Value")
            .AddContentReportFieldProvider("TimeField", ReportDataType.Text, ContentReportValueMode.Single, "Value")
            .AddContentReportFieldProvider("HtmlField", ReportDataType.Text, ContentReportValueMode.Single, "Html")
            .AddContentReportFieldProvider("MarkdownField", ReportDataType.Text, ContentReportValueMode.Single, "Markdown")
            .AddContentReportFieldProvider("MultiTextField", ReportDataType.Text, ContentReportValueMode.Join, "Values")
            .AddContentReportFieldProvider("MediaField", ReportDataType.Text, ContentReportValueMode.Join, "Paths")
            .AddContentReportFieldProvider("TaxonomyField", ReportDataType.Text, ContentReportValueMode.Join, "TermContentItemIds")
            .AddContentReportFieldProvider("LocalizationSetContentPickerField", ReportDataType.Text, ContentReportValueMode.Join, "LocalizationSets")
            .AddContentReportFieldProvider("YoutubeField", ReportDataType.Text, ContentReportValueMode.Single, "RawAddress")
            .AddContentReportFieldProvider("PhoneField", ReportDataType.Text, ContentReportValueMode.Single, "PhoneNumber");

        services.AddScoped<IContentReportFieldProvider, LinkFieldReportProvider>();
        services.AddScoped<IContentReportFieldProvider, ContentPickerFieldReportProvider>();
        services.AddScoped<IContentReportFieldProvider, UserPickerFieldReportProvider>();
        services.AddScoped<IContentReportPartProvider, CommonPartsReportProvider>();
    }
}
