using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Contents.Providers;

/// <summary>
/// Describes the data of the common Orchard Core parts: <c>TitlePart.Title</c>, <c>AutoroutePart.Path</c>,
/// <c>AliasPart.Alias</c>, <c>HtmlBodyPart.Html</c>, <c>MarkdownBodyPart.Markdown</c>,
/// <c>ContainedPart.ListContentItemId</c> (an identifier), <c>LocalizationPart.Culture</c>, and
/// <c>LocalizationPart.LocalizationSet</c> (an identifier). The prefix is the name of the part in the content type,
/// which is usually the part name itself.
/// </summary>
public sealed class CommonPartsReportProvider : IContentReportPartProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonPartsReportProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CommonPartsReportProvider(IStringLocalizer<CommonPartsReportProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.TypePartDefinition?.PartDefinition?.Name switch
        {
            "TitlePart" => [Text(context, "Title", S["Title"])],
            "AutoroutePart" => [Text(context, "Path", S["URL path"])],
            "AliasPart" => [Text(context, "Alias", S["Alias"])],
            "HtmlBodyPart" => [Text(context, "Html", S["HTML body"])],
            "MarkdownBodyPart" => [Text(context, "Markdown", S["Markdown body"])],
            "ContainedPart" => [Text(context, "ListContentItemId", S["Container ID"], isIdentifier: true)],
            "LocalizationPart" =>
            [
                Text(context, "Culture", S["Culture"]),
                Text(context, "LocalizationSet", S["Localization set"], isIdentifier: true),
            ],
            _ => [],
        };
    }

    private static ContentElementReportField Text(ContentReportFieldContext context, string propertyName, string displayName, bool isIdentifier = false)
    {
        var field = context.CreateElementField(propertyName, displayName, ReportDataType.Text, ContentReportValueMode.Single, propertyName);

        field.Descriptor.IsIdentifier = isIdentifier;

        return field;
    }
}
