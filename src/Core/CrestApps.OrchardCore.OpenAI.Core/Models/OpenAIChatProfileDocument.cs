using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class OpenAIChatProfileDocument : Document
{
    public Dictionary<string, OpenAIChatProfile> Profiles { get; set; } = [];
}
