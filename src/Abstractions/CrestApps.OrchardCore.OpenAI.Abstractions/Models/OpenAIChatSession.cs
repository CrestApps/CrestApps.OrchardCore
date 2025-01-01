using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class OpenAIChatSession : Entity
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public string Title { get; set; }

    public string WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the user id who created this session.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the client id who created this session when userId is not available.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this session was first created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
