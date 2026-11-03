namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// The query failed.
/// </summary>
public sealed class NLWebFailureEvent : NLWebEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebFailureEvent"/> class.
    /// </summary>
    /// <param name="code">The error code, such as <c>INVALID_REQUEST</c>.</param>
    /// <param name="message">The message for the caller.</param>
    public NLWebFailureEvent(
        string code,
        string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the message for the caller.
    /// </summary>
    public string Message { get; }
}
