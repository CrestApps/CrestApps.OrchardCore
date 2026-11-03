namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Sections of a report shown together: the sections of one container, or a section on its own.
/// </summary>
public sealed class ReportSectionGroup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportSectionGroup"/> class.
    /// </summary>
    /// <param name="container">The container that frames the sections, or <see langword="null"/> for a section on its own.</param>
    /// <param name="sections">The sections, in display order.</param>
    public ReportSectionGroup(ReportContainer container, IReadOnlyList<ReportSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        Container = container;
        Sections = sections;
    }

    /// <summary>
    /// Gets the container that frames the sections, or <see langword="null"/> for a section on its own.
    /// </summary>
    public ReportContainer Container { get; }

    /// <summary>
    /// Gets the sections, in display order.
    /// </summary>
    public IReadOnlyList<ReportSection> Sections { get; }
}
