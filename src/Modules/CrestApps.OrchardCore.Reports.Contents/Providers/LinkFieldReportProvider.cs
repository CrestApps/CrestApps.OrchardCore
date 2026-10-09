using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Contents.Providers;

/// <summary>
/// Describes a <c>LinkField</c>: <c>{PartName}.{FieldName}</c> is the URL and <c>{PartName}.{FieldName}.Text</c> is
/// the link text.
/// </summary>
public sealed class LinkFieldReportProvider : IContentReportFieldProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkFieldReportProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LinkFieldReportProvider(IStringLocalizer<LinkFieldReportProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string FieldType => "LinkField";

    /// <inheritdoc/>
    public IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            context.CreateElementField(null, context.DisplayName, ReportDataType.Text, ContentReportValueMode.Single, "Url"),
            context.CreateElementField("Text", S["{0} (link text)", context.DisplayName], ReportDataType.Text, ContentReportValueMode.Single, "Text"),
        ];
    }
}
