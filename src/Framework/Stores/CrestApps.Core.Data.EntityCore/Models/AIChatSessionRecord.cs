using CrestApps.Core.AI.Models;

namespace CrestApps.Core.Data.EntityCore.Models;

public sealed class AIChatSessionRecord
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public string Title { get; set; }

    public string UserId { get; set; }

    public string ClientId { get; set; }

    public ChatSessionStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime LastActivityUtc { get; set; }

    public string Payload { get; set; }
}
