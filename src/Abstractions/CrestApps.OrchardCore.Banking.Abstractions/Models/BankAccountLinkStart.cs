namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// What a link provider needs the browser to show to link a bank.
/// </summary>
public sealed class BankAccountLinkStart
{
    /// <summary>
    /// Gets or sets whether linking can start.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the shape Banking renders inside the link form.
    /// </summary>
    public string ShapeType { get; set; }

    /// <summary>
    /// Gets or sets what the shape needs, such as a short-lived token. Never stored.
    /// </summary>
    public string ClientPayload { get; set; }

    /// <summary>
    /// Gets or sets why linking can't start, for the person who asked.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Returns a start that is ready.
    /// </summary>
    /// <param name="shapeType">The shape to render.</param>
    /// <param name="clientPayload">What the shape needs.</param>
    /// <returns>The start.</returns>
    public static BankAccountLinkStart Ready(string shapeType, string clientPayload)
        => new() { Succeeded = true, ShapeType = shapeType, ClientPayload = clientPayload };

    /// <summary>
    /// Returns a start that was refused.
    /// </summary>
    /// <param name="message">Why.</param>
    /// <returns>The start.</returns>
    public static BankAccountLinkStart Refused(string message)
        => new() { Succeeded = false, Message = message };
}
