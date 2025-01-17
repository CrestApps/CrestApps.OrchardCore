using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI.Core;

public interface IAIToolsService
{
    /// <summary>
    /// Retrieves a specific AI function by its name.
    /// </summary>
    /// <param name="name">The name of the AI function to retrieve.</param>
    /// <returns>The <see cref="AIFunction"/> corresponding to the specified name, or <c>null</c> if no function is found.</returns>
    AIFunction GetFunction(string name);

    /// <summary>
    /// Retrieves all available AI functions.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="AIFunction"/> objects.</returns>
    IEnumerable<AIFunction> GetFunctions();

    /// <summary>
    /// Retrieves all available AI tools.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="AITool"/> objects.</returns>
    IEnumerable<AITool> GetTools();
}
