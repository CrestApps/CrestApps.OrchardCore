using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Query context for filtering subject actions.
/// </summary>
public sealed class SubjectActionQueryContext : QueryContext
{
    /// <summary>
    /// Gets or sets the subject content type to filter actions by.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the disposition identifier to filter actions by.
    /// </summary>
    public string DispositionId { get; set; }
}
