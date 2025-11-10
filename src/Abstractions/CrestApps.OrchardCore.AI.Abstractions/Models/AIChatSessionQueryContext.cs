using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIChatSessionQueryContext : QueryContext
{
    public string ProfileId { get; set; }

    public string UserId { get; set; }
}
