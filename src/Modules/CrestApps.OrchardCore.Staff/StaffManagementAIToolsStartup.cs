using CrestApps.Core.AI;
using CrestApps.OrchardCore.Staff.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff;

/// <summary>
/// Registers the staff record and capability AI tools whenever the Staff and AI features are on, beside the older
/// <c>staffFind</c> and <c>staffReportingLine</c>. Each checks the permission or the staff scope of the matching staff
/// page; the deletes ask for approval.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
public sealed class StaffManagementAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffManagementAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StaffManagementAIToolsStartup(IStringLocalizer<StaffManagementAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<StaffCapabilityToolScope>();

        services.AddCoreAITool<GetStaffMemberTool>(GetStaffMemberTool.TheName)
            .WithTitle(S["Get Staff Member"])
            .WithDescription(S["Returns one staff member's full record: employment details, manager, team memberships with dates and roles, capabilities, linked user, employment history and, when you may see them, date of birth and pay."])
            .WithCategory(S["Staff"])
            .Selectable();

        services.AddCoreAITool<SaveStaffMemberTool>(SaveStaffMemberTool.TheName)
            .WithTitle(S["Save Staff Member"])
            .WithDescription(S["Creates a staff member or updates one: job title, employment type, status, dates, manager, time zone, locations, contracted hours, capabilities and team memberships; can link a user account once. Setting a termination date or status Terminated starts offboarding in other modules."])
            .WithCategory(S["Staff"])
            .Selectable();

        services.AddCoreAITool<DeleteStaffMemberTool>(DeleteStaffMemberTool.TheName)
            .WithTitle(S["Delete Staff Member"])
            .WithDescription(S["Deletes a staff record. It is refused while people still report to them; set the status to Terminated instead to keep history."])
            .WithCategory(S["Staff"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListStaffCapabilitiesTool>(ListStaffCapabilitiesTool.TheName)
            .WithTitle(S["List Staff Capabilities"])
            .WithDescription(S["Lists the kinds of work staff can be given (such as Technician, Agent, Cashier), built-in and custom, with whether each is enabled."])
            .WithCategory(S["Staff"])
            .Selectable();

        services.AddCoreAITool<SaveStaffCapabilityTool>(SaveStaffCapabilityTool.TheName)
            .WithTitle(S["Save Staff Capability"])
            .WithDescription(S["Creates a custom capability or changes the display name, description, position and enabled flag of any capability."])
            .WithCategory(S["Staff"])
            .Selectable();

        services.AddCoreAITool<DeleteStaffCapabilityTool>(DeleteStaffCapabilityTool.TheName)
            .WithTitle(S["Delete Staff Capability"])
            .WithDescription(S["Deletes a custom capability, or resets a built-in one to its defaults. Staff who had a deleted capability keep it until they are next saved."])
            .WithCategory(S["Staff"])
            .RequireApproval()
            .Selectable();
    }
}
