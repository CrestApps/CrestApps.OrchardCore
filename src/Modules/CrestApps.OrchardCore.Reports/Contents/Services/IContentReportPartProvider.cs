using CrestApps.OrchardCore.Reports.Contents.Models;

namespace CrestApps.OrchardCore.Reports.Contents.Services;

/// <summary>
/// Describes the report fields a content part contributes from its own data (for example the <c>Title</c> of the
/// <c>TitlePart</c>), apart from the content fields attached to it. Register an implementation as a scoped or
/// singleton <see cref="IContentReportPartProvider"/>. Every provider is asked about every part of a content type and
/// returns nothing for the parts it does not know; the fields of all providers are combined. Name the fields
/// <c>{PartName}.{Property}</c> through <see cref="ContentReportFieldContext.GetFieldName(string)"/>.
/// </summary>
public interface IContentReportPartProvider
{
    /// <summary>
    /// Describes the report fields of one part of a content type.
    /// </summary>
    /// <param name="context">The part to describe. <see cref="ContentReportFieldContext.PartFieldDefinition"/> is <see langword="null"/>.</param>
    /// <returns>The report fields, or an empty sequence when the provider does not handle the part.</returns>
    IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context);
}
