using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.SourceAccess;

namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// What a page of disbursements shows.
/// </summary>
public sealed class DisbursementQuery
{
    /// <summary>
    /// Gets or sets the status, or <see langword="null"/> for every status.
    /// </summary>
    public DisbursementStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the method, or <see langword="null"/> for every method.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the batch, or <see langword="null"/> for every batch.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets text the payee's name contains, or a trace number.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the oldest come first; the newest do by default.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// Gets or sets the disbursements the reader may see.
    /// </summary>
    public SourceAccessFilter Access { get; set; } = SourceAccessFilter.Unrestricted;
}
