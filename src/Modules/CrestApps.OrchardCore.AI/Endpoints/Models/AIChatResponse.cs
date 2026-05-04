using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Endpoints.Models;

internal sealed class AIChatResponse
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public AIChatResponseMessageDetailed Message { get; set; }
}
