namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// Something an agent checkout tells the agent: an error to fix, a warning or information.
/// </summary>
public sealed class AgentCheckoutMessage
{
    /// <summary>
    /// Gets or sets the kind of message.
    /// </summary>
    public AgentMessageType Type { get; set; }

    /// <summary>
    /// Gets or sets the message's code, one of <see cref="AgentMessageCodes"/>.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the field the message is about, one of <see cref="AgentCheckoutFields"/>, or <see langword="null"/>.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the message, for the buyer.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets who can resolve an error.
    /// </summary>
    public AgentMessageSeverity Severity { get; set; }

    /// <summary>
    /// Returns an error message.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="field">The field it is about.</param>
    /// <param name="content">The message.</param>
    /// <param name="severity">Who can resolve it.</param>
    public static AgentCheckoutMessage Error(string code, string field, string content, AgentMessageSeverity severity = AgentMessageSeverity.Recoverable)
        => new()
        {
            Type = AgentMessageType.Error,
            Code = code,
            Field = field,
            Content = content,
            Severity = severity,
        };

    /// <summary>
    /// Returns a warning.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="field">The field it is about.</param>
    /// <param name="content">The message.</param>
    public static AgentCheckoutMessage Warning(string code, string field, string content)
        => new()
        {
            Type = AgentMessageType.Warning,
            Code = code,
            Field = field,
            Content = content,
        };
}
