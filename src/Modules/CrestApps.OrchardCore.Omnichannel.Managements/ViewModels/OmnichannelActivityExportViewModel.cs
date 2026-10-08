using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// The editor view model for the "include last completed activity" bulk export option.
/// </summary>
public class OmnichannelActivityExportViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the export should include each contact's last completed
    /// activity information.
    /// </summary>
    public bool IncludeLastActivity { get; set; }

    /// <summary>
    /// Gets or sets the selected subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include only contacts that have a matching last completed
    /// activity of the selected subject.
    /// </summary>
    public bool OnlyContactsWithLastActivity { get; set; }

    /// <summary>
    /// Gets or sets the available subject content types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the content type names this option applies to (contact content types). Used by the
    /// export form to show the option only when a contact type is selected.
    /// </summary>
    [BindNever]
    public IReadOnlyCollection<string> ContactContentTypes { get; set; }
}
