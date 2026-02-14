using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Core;

public interface IAIToolsService
{
    /// <summary>
    /// Retrieves an AI tool by its name.
    /// </summary>
    /// <param name="name">The name of the AI tool.</param>
    /// <returns>A task that represents the asynchronous operation, containing the retrieved <see cref="AITool"/>.</returns>
    ValueTask<AITool> GetByNameAsync(string name);
}
