using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The Payroll card in the Pay section of a staff member's profile: their pay profiles with what keeps each from being
/// paid, for people who may see the pay profiles of that employer.
/// </summary>
internal sealed class StaffPayrollDisplayDriver : DisplayDriver<StaffMember>
{
    private readonly IPayProfileManager _profiles;
    private readonly IPayProfileService _service;
    private readonly IPayrollScopeService _scope;
    private readonly PayrollNames _names;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StaffPayrollDisplayDriver(IPayProfileManager profiles, IPayProfileService service, IPayrollScopeService scope, PayrollNames names, IHttpContextAccessor httpContextAccessor)
    {
        _profiles = profiles;
        _service = service;
        _scope = scope;
        _names = names;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(StaffMember staff, BuildDisplayContext context)
    {
        if (context.DisplayType != StaffProfileSections.DisplayType)
        {
            return null;
        }

        var user = _httpContextAccessor.HttpContext?.User;
        var view = await _scope.GetScopeAsync(user, PayrollPermissions.ViewPayProfiles);
        var manage = await _scope.GetScopeAsync(user, PayrollPermissions.ManagePayProfiles);

        if (view.IsEmpty)
        {
            return null;
        }

        var rows = new List<PayProfileRow>();

        foreach (var profile in (await _profiles.ListByStaffMemberAsync(staff.ItemId)).Where(profile => view.Allows(profile.EmployerId)))
        {
            rows.Add(new PayProfileRow
            {
                Profile = profile,
                EmployerName = await _names.EmployerAsync(profile.EmployerId),
                ScheduleName = await _names.ScheduleAsync(profile.PayScheduleId),
                Blockers = await _service.GetBlockersAsync(profile),
            });
        }

        var canAdd = !manage.IsEmpty && await _service.MayChangePayOfAsync(user, staff.ItemId);

        return Initialize<StaffPayrollCardViewModel>("StaffPayrollCard", model =>
        {
            model.StaffMemberId = staff.ItemId;
            model.Profiles = rows;
            model.CanAdd = canAdd;
        }).Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Pay, "5"));
    }
}
