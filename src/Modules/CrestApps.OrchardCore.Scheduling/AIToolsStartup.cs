using CrestApps.Core.AI;
using CrestApps.OrchardCore.Scheduling.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Registers the scheduling AI tools, which administrators can pick on AI profiles.
/// </summary>
[Feature(SchedulingConstants.Features.AITools)]
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
        services.AddCoreAITool<ListAppointmentTypesTool>(ListAppointmentTypesTool.ToolName).WithTitle(S["List appointment types"]).WithDescription(S["Lists the kinds of appointments that can be booked."]).WithCategory(S["Scheduling"]).Selectable();
        services.AddCoreAITool<FindAppointmentSlotsTool>(FindAppointmentSlotsTool.ToolName).WithTitle(S["Find appointment slots"]).WithDescription(S["Finds open times for an appointment."]).WithCategory(S["Scheduling"]).Selectable();
        services.AddCoreAITool<BookAppointmentTool>(BookAppointmentTool.ToolName).WithTitle(S["Book appointment"]).WithDescription(S["Books an appointment for the customer."]).WithCategory(S["Scheduling"]).Selectable();
        services.AddCoreAITool<ChangeAppointmentTool>(ChangeAppointmentTool.ToolName).WithTitle(S["Change appointment"]).WithDescription(S["Cancels or moves a customer's appointment."]).WithCategory(S["Scheduling"]).Selectable();
    }
}
