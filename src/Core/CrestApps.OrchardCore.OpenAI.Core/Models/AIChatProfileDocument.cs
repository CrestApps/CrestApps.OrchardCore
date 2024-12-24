using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AIChatProfileDocument : Document
{
    public Dictionary<string, AIChatProfile> Profiles { get; set; } = [];
}
