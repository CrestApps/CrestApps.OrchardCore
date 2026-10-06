namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// One call a dialer profile placed, as <c>DialerPacingQueries.BuildCallTimingsSql</c> returns it.
/// </summary>
public sealed class DialerCallTimingRow
{
    /// <summary>
    /// Gets or sets the identifier of the call's interaction.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets when the call was placed.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the dialer learned a person answered, if one did.
    /// </summary>
    public DateTime? LiveAnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets when an agent was first connected, if one was.
    /// </summary>
    public DateTime? AgentJoinedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the call ended, if it has.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the agent started wrapping the call up, if they have.
    /// </summary>
    public DateTime? WrapUpStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the agent finished wrapping the call up, if they have.
    /// </summary>
    public DateTime? WrapUpCompletedUtc { get; set; }
}
