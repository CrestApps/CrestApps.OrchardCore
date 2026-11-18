namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A ticket record that is shared with everybody, with a team, or kept by its owner: a saved view or a board.
/// </summary>
public interface ITicketSharedRecord : ITicketSettingsRecord
{
    /// <summary>
    /// The member of a record's recipe JSON that names its owner by user name, since a user id differs from site to
    /// site.
    /// </summary>
    const string OwnerUserNameProperty = "OwnerUserName";

    /// <summary>
    /// Gets or sets the user who keeps it; empty for a record everybody (or a team) shares.
    /// </summary>
    string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the team it is shared with; empty shares it with everybody (or nobody, when it has an owner).
    /// </summary>
    string SharedWithTeamId { get; set; }
}
