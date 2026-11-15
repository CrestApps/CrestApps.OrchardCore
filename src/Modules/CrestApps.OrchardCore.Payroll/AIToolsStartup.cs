using CrestApps.Core.AI;
using CrestApps.OrchardCore.Payroll.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Adds the Payroll AI tools: a person's own pay statement explained, and what changed in a pay run since the last one.
/// </summary>
[Feature(PayrollConstants.Features.AI)]
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
        services.AddCoreAITool<MyPayStatementTool>(MyPayStatementTool.ToolName)
            .WithTitle(S["Explain my pay statement"])
            .WithDescription(S["Explains the user's own pay statement and what changed since the pay before it."])
            .WithCategory(S["Payroll"])
            .Selectable();

        services.AddCoreAITool<PayRunChangesTool>(PayRunChangesTool.ToolName)
            .WithTitle(S["What changed since the last pay run"])
            .WithDescription(S["Compares a pay run with the run before it, for the employers the user may see."])
            .WithCategory(S["Payroll"])
            .Selectable();
    }
}
