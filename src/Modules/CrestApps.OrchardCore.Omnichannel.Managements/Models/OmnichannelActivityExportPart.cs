namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// Stores the "include last completed activity" export option on a content transfer export entry so the
/// export background task can append the contact's most recent completed activity of the chosen subject.
/// </summary>
public sealed class OmnichannelActivityExportPart
{
    /// <summary>
    /// Gets or sets a value indicating whether the export should include each contact's last completed
    /// activity information.
    /// </summary>
    public bool IncludeLastActivity { get; set; }

    /// <summary>
    /// Gets or sets the subject content type whose last completed activity should be exported.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the export should include only contacts that have a matching
    /// last completed activity of the selected subject, omitting contacts without one.
    /// </summary>
    public bool OnlyContactsWithLastActivity { get; set; }
}
