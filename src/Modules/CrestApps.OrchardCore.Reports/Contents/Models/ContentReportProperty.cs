using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Contents.Models;

/// <summary>
/// A property a content part stores, described by an <see cref="Services.IContentReportPropertySource"/>.
/// </summary>
/// <param name="Path">The path of the property within the part, with dots between nested names, such as <c>Path</c> or <c>Settings.Mode</c>.</param>
/// <param name="DataType">The type of its values.</param>
public sealed record ContentReportProperty(string Path, ReportDataType DataType);
