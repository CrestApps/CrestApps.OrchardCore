using CrestApps.Core.AI;
using CrestApps.OrchardCore.Staff.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff;

/// <summary>
/// Adds the Staff AI tools when AI is on.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<FindStaffTool>(FindStaffTool.ToolName)
            .WithTitle(S["Find staff"])
            .WithDescription(S["Finds the people who work for the business by name, job title, team or capability."])
            .WithCategory(S["Staff"])
            .Selectable();

        services.AddCoreAITool<ReportingLineTool>(ReportingLineTool.ToolName)
            .WithTitle(S["Reporting line"])
            .WithDescription(S["Gives who a person reports to and who reports to them."])
            .WithCategory(S["Staff"])
            .Selectable();
    }
}
