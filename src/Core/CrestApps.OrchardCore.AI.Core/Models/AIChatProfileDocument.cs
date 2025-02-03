using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AI.Azure.Core.Models;

public sealed class AIChatProfileDocument : Document
{
    public Dictionary<string, AIChatProfile> Profiles { get; set; } = [];
}
