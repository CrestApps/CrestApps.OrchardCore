namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// The kind of an agent checkout message.
/// </summary>
public enum AgentMessageType
{
    /// <summary>
    /// Something stops the checkout from completing.
    /// </summary>
    Error,

    /// <summary>
    /// Something the buyer should know, such as a changed price.
    /// </summary>
    Warning,

    /// <summary>
    /// Information only.
    /// </summary>
    Info,
}
