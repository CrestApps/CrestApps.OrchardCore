using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Contents.Providers;

/// <summary>
/// A field provider for content fields whose report value is one JSON property, such as the <c>Text</c> of a
/// <c>TextField</c>. It contributes a single report field named <c>{PartName}.{FieldName}</c>.
/// </summary>
public sealed class PropertyContentReportFieldProvider : IContentReportFieldProvider
{
    private readonly ReportDataType _dataType;
    private readonly ContentReportValueMode _mode;
    private readonly string[] _propertyNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyContentReportFieldProvider"/> class.
    /// </summary>
    /// <param name="fieldType">The technical name of the content field type, such as <c>TextField</c>.</param>
    /// <param name="dataType">The data type of the report field.</param>
    /// <param name="mode">How the property becomes a value.</param>
    /// <param name="propertyNames">The JSON property names to try, in order.</param>
    public PropertyContentReportFieldProvider(
        string fieldType,
        ReportDataType dataType,
        ContentReportValueMode mode,
        params string[] propertyNames)
    {
        FieldType = fieldType;
        _dataType = dataType;
        _mode = mode;
        _propertyNames = propertyNames;
    }

    /// <inheritdoc/>
    public string FieldType { get; }

    /// <inheritdoc/>
    public IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return [context.CreateElementField(null, context.DisplayName, _dataType, _mode, _propertyNames)];
    }
}
