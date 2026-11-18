using CrestApps.Core.AI;
using CrestApps.OrchardCore.Staff.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff;

/// <summary>
/// Registers the badge PIN AI tool whenever the Badges and AI features are on. It never takes or returns a PIN, and
/// asks for approval.
/// </summary>
[Feature(StaffConstants.Features.Badges)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class BadgesAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BadgesAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BadgesAIToolsStartup(IStringLocalizer<BadgesAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SetStaffBadgePinTool>(SetStaffBadgePinTool.TheName)
            .WithTitle(S["Clear Staff Badge PIN"])
            .WithDescription(S["Clears the PIN a staff member uses with their employee number at time clocks and registers, or says whether one is set. A PIN is never taken or returned: a new PIN is set on the staff editor, whose address the result gives."])
            .WithCategory(S["Staff"])
            .RequireApproval()
            .Selectable();
    }
}
