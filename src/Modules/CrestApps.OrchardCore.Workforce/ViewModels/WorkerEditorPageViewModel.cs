using CrestApps.OrchardCore.Staff.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The worker editor page: the editor shape the worker's display drivers build, and the certificates shown under it.
/// </summary>
public class WorkerEditorPageViewModel
{
    /// <summary>
    /// Gets or sets the worker id, or empty for a new worker.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the worker's name, for the page title.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the editor shape, with a section from each display driver of <c>StaffMember</c>.
    /// </summary>
    public object Editor { get; set; }

    /// <summary>
    /// Gets or sets the worker's certificates, shown read-only.
    /// </summary>
    public IList<WorkerCertificateViewModel> Certificates { get; set; } = [];

    /// <summary>
    /// Gets or sets the certifications to add a certificate of.
    /// </summary>
    public IList<SelectListItem> CertificationOptions { get; set; } = [];
}
