namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents the uniform result of running a report: an ordered set of sections (metrics, tables, and
/// bars) that the shared renderer displays and the exporter serializes.
/// </summary>
public sealed class ReportDocument
{
    /// <summary>
    /// Gets or sets the report title used for exported document metadata such as worksheet names.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the sections that make up the report, in display order.
    /// </summary>
    public IList<ReportSection> Sections { get; set; } = [];

    /// <summary>
    /// Gets or sets the containers that frame sections on the report page. <see cref="GetSectionGroups"/> groups the
    /// sections by them.
    /// </summary>
    public IList<ReportContainer> Containers { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the report has any content to display.
    /// </summary>
    public bool HasContent
    {
        get
        {
            return Sections.Count > 0;
        }
    }

    /// <summary>
    /// Adds a section to the report and returns the same document for chaining.
    /// </summary>
    /// <param name="section">The section to add. Ignored when <see langword="null"/>.</param>
    /// <returns>The current document.</returns>
    public ReportDocument Add(ReportSection section)
    {
        if (section is not null)
        {
            Sections.Add(section);
        }

        return this;
    }

    /// <summary>
    /// Groups the sections as the report page shows them: a container is placed where its first section is and holds
    /// all of its sections, and a section outside a container is a group of its own.
    /// </summary>
    /// <returns>The groups, in display order.</returns>
    public IReadOnlyList<ReportSectionGroup> GetSectionGroups()
    {
        var containers = Containers
            .Where(container => !string.IsNullOrEmpty(container?.Id))
            .DistinctBy(container => container.Id, StringComparer.Ordinal)
            .ToDictionary(container => container.Id, StringComparer.Ordinal);
        var groups = new List<ReportSectionGroup>();
        var placed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in Sections)
        {
            if (section is null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(section.ContainerId) || !containers.TryGetValue(section.ContainerId, out var container))
            {
                groups.Add(new ReportSectionGroup(null, [section]));

                continue;
            }

            if (placed.Add(container.Id))
            {
                groups.Add(new ReportSectionGroup(container, [.. Sections.Where(candidate => candidate?.ContainerId == container.Id)]));
            }
        }

        return groups;
    }

    /// <summary>
    /// Gets the sections whose picked values filter a section: the sections with values picked now that target it.
    /// </summary>
    /// <param name="section">The section filtered.</param>
    /// <returns>The sections, in display order.</returns>
    public IReadOnlyList<ReportSection> GetFilteringSections(ReportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        if (string.IsNullOrEmpty(section.Id))
        {
            return [];
        }

        return
        [
            .. Sections.Where(candidate => candidate?.Selection is { SelectedKeys.Count: > 0 } selection &&
                selection.TargetIds.Contains(section.Id, StringComparer.Ordinal)),
        ];
    }
}
