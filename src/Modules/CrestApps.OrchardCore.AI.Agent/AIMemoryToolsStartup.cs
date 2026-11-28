using CrestApps.Core.AI;
using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore.AI.Agent.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that clears a user's AI memory, when AI Memory is on.
/// </summary>
[RequireFeatures(MemoryConstants.Feature.Memory)]
public sealed class AIMemoryToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIMemoryToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIMemoryToolsStartup(IStringLocalizer<AIMemoryToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ClearAIUserMemoryTool>(ClearAIUserMemoryTool.TheName)
            .WithTitle(S["Clear AI User Memory"])
            .WithDescription(S["Deletes everything the AI has remembered about one user and returns how many memories were removed. It cannot be undone."])
            .WithCategory(S["AI Memory"])
            .AsDestructive()
            .Selectable();
    }
}
