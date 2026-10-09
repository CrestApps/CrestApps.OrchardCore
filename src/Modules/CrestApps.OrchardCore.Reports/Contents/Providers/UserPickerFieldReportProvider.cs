using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Contents.Providers;

/// <summary>
/// Describes a <c>UserPickerField</c>: <c>{PartName}.{FieldName}</c> is the first picked user ID (an identifier),
/// <c>{PartName}.{FieldName}.UserIds</c> is every picked user ID, and <c>{PartName}.{FieldName}.UserNames</c> is every
/// picked user name, each joined with commas.
/// </summary>
public sealed class UserPickerFieldReportProvider : IContentReportFieldProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPickerFieldReportProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UserPickerFieldReportProvider(IStringLocalizer<UserPickerFieldReportProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string FieldType => "UserPickerField";

    /// <inheritdoc/>
    public IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var first = context.CreateElementField(null, context.DisplayName, ReportDataType.Text, ContentReportValueMode.First, "UserIds");

        first.Descriptor.IsIdentifier = true;

        return
        [
            first,
            context.CreateElementField("UserIds", S["{0} (all user IDs)", context.DisplayName], ReportDataType.Text, ContentReportValueMode.Join, "UserIds"),
            context.CreateElementField("UserNames", S["{0} (user names)", context.DisplayName], ReportDataType.Text, ContentReportValueMode.Join, "UserNames"),
        ];
    }
}
