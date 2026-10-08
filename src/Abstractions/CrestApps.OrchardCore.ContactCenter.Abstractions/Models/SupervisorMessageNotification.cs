namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// A short message a supervisor sent an agent, shown on the agent's soft phone.
/// </summary>
public sealed class SupervisorMessageNotification
{
    /// <summary>
    /// Gets or sets the message's identifier.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the agent's user identifier, whose connections receive the message.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the agent's agent-profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the name of the supervisor who sent it.
    /// </summary>
    public string FromName { get; set; }

    /// <summary>
    /// Gets or sets the message text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was sent.
    /// </summary>
    public DateTime SentUtc { get; set; }
}
