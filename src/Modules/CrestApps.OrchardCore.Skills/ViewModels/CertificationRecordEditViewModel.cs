using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// The editor of one person's certificate.
/// </summary>
public class CertificationRecordEditViewModel
{
    /// <summary>
    /// Gets or sets the record id, or empty for a new record.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the user who holds the certificate.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the certification.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the certificate number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the day it was issued.
    /// </summary>
    public DateOnly? IssuedOn { get; set; }

    /// <summary>
    /// Gets or sets the last day it is valid; empty to work it out from the certification.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the media library path of a copy of the certificate.
    /// </summary>
    public string DocumentPath { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets whether the holder is a workforce worker, edited from the worker instead.
    /// </summary>
    [BindNever]
    public bool HeldByWorker { get; set; }

    /// <summary>
    /// Gets or sets the holder's name, for a worker's record.
    /// </summary>
    [BindNever]
    public string SubjectName { get; set; }

    /// <summary>
    /// Gets or sets the certifications to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CertificationOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the states to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];
}
