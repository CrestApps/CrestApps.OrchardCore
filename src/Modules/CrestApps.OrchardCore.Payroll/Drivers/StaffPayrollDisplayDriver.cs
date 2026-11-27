using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The Payroll card in the Pay section of a staff member's profile: their pay profiles with what keeps each from being
/// paid, for people who may see the pay profiles of that employer. A person has one pay profile with each employer, so
/// the company's own payroll offers to add one only until they have it, and Multiple Employers until they have one with
/// every employer the user manages.
/// </summary>
internal sealed class StaffPayrollDisplayDriver : DisplayDriver<StaffMember>
{
    private readonly IPayProfileManager _profiles;
    private readonly IPayProfileService _service;
    private readonly IPayrollScopeService _scope;
    private readonly PayrollNames _names;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPayrollCompanyService _company;
    private readonly EmployerStore _employers;
    private readonly PayrollEmployerOptions _employerOptions;

    public StaffPayrollDisplayDriver(
        IPayProfileManager profiles,
        IPayProfileService service,
        IPayrollScopeService scope,
        PayrollNames names,
        IHttpContextAccessor httpContextAccessor,
        IPayrollCompanyService company,
        EmployerStore employers,
        IOptions<PayrollEmployerOptions> employerOptions)
    {
        _profiles = profiles;
        _service = service;
        _scope = scope;
        _names = names;
        _httpContextAccessor = httpContextAccessor;
        _company = company;
        _employers = employers;
        _employerOptions = employerOptions.Value;
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

        // Pay is private: until the viewer confirms their password, the card is the one prompt, or nothing.
        switch (await StaffPayPrivacy.DecideAsync(_httpContextAccessor.HttpContext, staff))
        {
            case StaffPayVisibility.Hide:
                return null;
            case StaffPayVisibility.Prompt:
                return Dynamic(StaffPayPrivacy.PromptShape, shape => { shape.ConfirmUrl = StaffPayPrivacy.ConfirmUrl(_httpContextAccessor.HttpContext); })
                    .Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Pay, "0"));
        }

        var rows = new List<PayProfileRow>();
        var all = await _profiles.ListByStaffMemberAsync(staff.ItemId);

        foreach (var profile in all.Where(profile => view.Allows(profile.EmployerId)))
        {
            rows.Add(new PayProfileRow
            {
                Profile = profile,
                EmployerName = await _names.EmployerAsync(profile.EmployerId),
                ScheduleName = await _names.ScheduleAsync(profile.PayScheduleId),
                Blockers = await _service.GetBlockersAsync(profile),
            });
        }

        // The employers the person could still get a pay profile with: the company, or with Multiple Employers every
        // active employer the user manages profiles for.
        var paidBy = all.Select(profile => profile.EmployerId).ToHashSet(StringComparer.Ordinal);
        IEnumerable<Employer> employers = _employerOptions.MultipleEmployers
            ? (await _employers.ListAsync()).Where(employer => employer.Status == EmployerStatus.Active)
            : await _company.GetCompanyAsync() is { } company ? [company] : [];
        var open = employers.Any(employer => manage.Allows(employer.ItemId) && !paidBy.Contains(employer.ItemId))
            || (!_employerOptions.MultipleEmployers && paidBy.Count == 0);
        var canAdd = open && !manage.IsEmpty && await _service.MayChangePayOfAsync(user, staff.ItemId);

        return Initialize<StaffPayrollCardViewModel>("StaffPayrollCard", model =>
        {
            model.StaffMemberId = staff.ItemId;
            model.Profiles = rows;
            model.CanAdd = canAdd;
            model.ShowEmployer = _employerOptions.MultipleEmployers;
        }).Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Pay, "5"));
    }
}
