using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// With Multiple Employers on: the employer of a pay profile, the company by default, at the top of the profile's card.
/// A person has one pay profile with each employer. A tax id typed with it is checked against the employer chosen.
/// </summary>
internal sealed class PayProfileEmployerDisplayDriver : DisplayDriver<EmployeePayProfile>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPayrollScopeService _scope;
    private readonly EmployerStore _employers;
    private readonly IPayProfileManager _profiles;

    internal readonly IStringLocalizer S;

    public PayProfileEmployerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IPayrollScopeService scope,
        EmployerStore employers,
        IPayProfileManager profiles,
        IStringLocalizer<PayProfileEmployerDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _scope = scope;
        _employers = employers;
        _profiles = profiles;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(EmployeePayProfile profile, BuildEditorContext context)
    {
        // My Pay shows the person's own elections only.
        if (context.GroupId == PayrollConstants.MyPayEditorGroup)
        {
            return null;
        }

        return Initialize<PayrollEmployerViewModel>("PayrollEmployer_Edit", async model =>
        {
            var manage = await _scope.GetScopeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.ManagePayProfiles);
            var employers = (await _employers.ListAsync()).Where(employer => manage.Allows(employer.ItemId) || employer.ItemId == profile.EmployerId).ToList();
            var chosen = profile.EmployerId;

            // A new profile of someone the company already pays starts with an employer that doesn't pay them yet.
            if (context.IsNew && !string.IsNullOrEmpty(profile.StaffMemberId))
            {
                var paidBy = (await _profiles.ListByStaffMemberAsync(profile.StaffMemberId)).Select(other => other.EmployerId).ToHashSet(StringComparer.Ordinal);

                if (chosen is null || paidBy.Contains(chosen))
                {
                    chosen = employers.FirstOrDefault(employer => employer.Status == EmployerStatus.Active && !paidBy.Contains(employer.ItemId))?.ItemId ?? chosen;
                }
            }

            model.EmployerId = chosen;
            model.Employers = [.. employers.Select(employer => new SelectListItem(employer.Name, employer.ItemId, employer.ItemId == chosen))];
            model.Hint = S["The legal entity that employs and pays the person. A person has one pay profile with each employer."];
        }).Location(l => l.Zone("Content", "0").Card(S["Pay Profile"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(EmployeePayProfile profile, UpdateEditorContext context)
    {
        if (context.GroupId == PayrollConstants.MyPayEditorGroup)
        {
            return null;
        }

        var model = new PayrollEmployerViewModel();
        var typed = new PayProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        await context.Updater.TryUpdateModelAsync(typed, Prefix);

        if (string.IsNullOrEmpty(model.EmployerId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.EmployerId), S["Choose the employer."]);

            return Edit(profile, context);
        }

        // The profile's own driver checked a typed social insurance number against the employer the profile had; check
        // it against the employer chosen here too.
        if (model.EmployerId != profile.EmployerId && !string.IsNullOrWhiteSpace(typed.TaxId)
            && PayProfileDisplayDriver.RefusesTaxId(await _employers.FindByIdAsync(model.EmployerId), typed.TaxId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(typed.TaxId), S["The social insurance number is not valid: check its nine digits."]);
        }

        profile.EmployerId = model.EmployerId;

        return Edit(profile, context);
    }
}

/// <summary>
/// With Multiple Employers on: the employer picker of the payroll pages' filters (pay runs, liabilities, filings and
/// year end), shown when the user may choose among more than one employer.
/// </summary>
internal sealed class PayrollEmployerFilterDisplayDriver : DisplayDriver<PayrollEmployerFilter>
{
    public override IDisplayResult Display(PayrollEmployerFilter filter, BuildDisplayContext context)
    {
        // A page of the only employer the user may see needs no picker, except the card that chooses it.
        if (filter.Employers.Count < 2 && context.DisplayType != PayrollEmployerFilterDisplayTypes.Card)
        {
            return null;
        }

        var shapeType = context.DisplayType switch
        {
            PayrollEmployerFilterDisplayTypes.Toolbar => "PayrollEmployerSelect_Toolbar",
            PayrollEmployerFilterDisplayTypes.Card => "PayrollEmployerSelect_Card",
            _ => "PayrollEmployerSelect_Field",
        };

        return View(shapeType, filter).Location("Content:1");
    }
}
