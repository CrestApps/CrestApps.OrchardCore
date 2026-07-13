using System.Net;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Resolves typed report values through the Orchard display pipeline before display or export.
/// </summary>
public sealed class ReportDisplayValueResolver
{
    private readonly IShapeFactory _shapeFactory;
    private readonly IDisplayHelper _displayHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDisplayValueResolver"/> class.
    /// </summary>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="displayHelper">The display helper.</param>
    public ReportDisplayValueResolver(
        IShapeFactory shapeFactory,
        IDisplayHelper displayHelper)
    {
        _shapeFactory = shapeFactory;
        _displayHelper = displayHelper;
    }

    /// <summary>
    /// Resolves all typed values in a report document.
    /// </summary>
    /// <param name="document">The report document.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ResolveAsync(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var userDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in document.Sections)
        {
            await ResolveNonTableValuesAsync(section, userDisplayNames);

            foreach (var row in section.Rows)
            {
                for (var index = 0; index < row.Cells.Count; index++)
                {
                    row.Cells[index] = await ResolveAsync(row.Cells[index], userDisplayNames);
                }
            }

        }
    }

    /// <summary>
    /// Resolves typed values that cannot render an Orchard shape directly in the report view.
    /// Table cells remain typed so the view can render each user value as its own shape.
    /// </summary>
    /// <param name="document">The report document.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ResolveNonTableValuesAsync(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var userDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in document.Sections)
        {
            await ResolveNonTableValuesAsync(section, userDisplayNames);
        }
    }

    private async Task ResolveNonTableValuesAsync(
        ReportSection section,
        Dictionary<string, string> userDisplayNames)
    {
        for (var index = 0; index < section.Metrics.Count; index++)
        {
            var metric = section.Metrics[index];
            metric.Label = await ResolveAsync(metric.Label, userDisplayNames);
            metric.Value = await ResolveAsync(metric.Value, userDisplayNames);
            metric.Hint = await ResolveAsync(metric.Hint, userDisplayNames);
        }

        for (var index = 0; index < section.Bars.Count; index++)
        {
            var bar = section.Bars[index];
            bar.Label = await ResolveAsync(bar.Label, userDisplayNames);
            bar.Value = await ResolveAsync(bar.Value, userDisplayNames);
        }

        if (section.Chart is null)
        {
            return;
        }

        for (var index = 0; index < section.Chart.Labels.Count; index++)
        {
            section.Chart.Labels[index] = await ResolveAsync(section.Chart.Labels[index], userDisplayNames);
        }

        foreach (var dataset in section.Chart.Datasets)
        {
            dataset.Label = await ResolveAsync(dataset.Label, userDisplayNames);
        }
    }

    private async Task<string> ResolveAsync(
        string value,
        Dictionary<string, string> userDisplayNames)
    {
        if (!ReportValue.TryGetUserName(value, out var userName))
        {
            return value;
        }

        if (userDisplayNames.TryGetValue(userName, out var displayName))
        {
            return displayName;
        }

        var shape = await _shapeFactory.CreateAsync(
            "ReportUserDisplayName",
            Arguments.From(new
            {
                UserName = userName,
            }));
        var content = await _displayHelper.ShapeExecuteAsync(shape);
        using var writer = new StringWriter();

        content.WriteTo(writer, HtmlEncoder.Default);

        displayName = WebUtility.HtmlDecode(writer.ToString()).Trim();
        userDisplayNames[userName] = displayName;

        return displayName;
    }
}
