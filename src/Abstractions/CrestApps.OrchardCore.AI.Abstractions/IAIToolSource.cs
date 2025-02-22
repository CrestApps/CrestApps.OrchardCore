using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI;

public interface IAIToolSource
{
    string Name { get; }

    AIToolSourceType Type { get; }

    LocalizedString DisplayName { get; }

    LocalizedString Description { get; }

    Task<AITool> CreateAsync(AIToolInstance instance);
}

public enum AIToolSourceType
{
    Tool,
    Function,
}
