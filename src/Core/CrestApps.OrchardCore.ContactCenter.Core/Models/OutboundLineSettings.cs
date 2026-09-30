namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Makes a phone channel endpoint a line that agents dial out from. It is stored in the endpoint's properties, so
/// the tenant's numbers stay one managed list and a line is simply a number with agents assigned to it.
/// </summary>
public sealed class OutboundLineSettings
{
    /// <summary>
    /// Gets or sets the identifiers of the users who dial out from this number. A user is assigned to at most one line.
    /// </summary>
    public IList<string> UserIds { get; set; } = [];
}
