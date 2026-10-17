using CrestApps.OrchardCore.Reports.Contents.Models;

namespace CrestApps.OrchardCore.Reports.Contents.Services;

/// <summary>
/// Describes the report fields one type of content field (for example <c>TextField</c>) contributes to a content
/// type data set, and how their values are read. Register an implementation as a scoped or singleton
/// <see cref="IContentReportFieldProvider"/>. When several providers handle the same field type, the one registered
/// last wins, so a module can replace a built-in provider. A content field no provider handles falls back to a
/// generic text field that reads the <c>Text</c> or <c>Value</c> property of the field.
/// </summary>
public interface IContentReportFieldProvider
{
    /// <summary>
    /// Gets the technical name of the content field type this provider handles, such as <c>TextField</c>.
    /// </summary>
    string FieldType { get; }

    /// <summary>
    /// Describes the report fields of one content field. The first field should use the base name
    /// (<see cref="ContentReportFieldContext.Name"/>); extra values use a suffix. Names must not change between
    /// releases because report designs store them.
    /// </summary>
    /// <param name="context">The content field to describe.</param>
    /// <returns>The report fields.</returns>
    IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context);
}
