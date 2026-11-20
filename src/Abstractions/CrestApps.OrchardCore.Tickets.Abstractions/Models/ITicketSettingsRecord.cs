using CrestApps.Core;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A ticket settings record: a status, a priority, a desk or a saved view.
/// </summary>
public interface ITicketSettingsRecord : INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the record's name.
    /// </summary>
    new string Name { get; set; }

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    DateTime CreatedUtc { get; set; }
}
