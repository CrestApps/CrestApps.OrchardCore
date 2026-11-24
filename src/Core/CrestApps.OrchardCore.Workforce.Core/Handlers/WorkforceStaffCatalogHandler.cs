using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Keeps Workforce's scheduling values on a staff member: writes changes made in place (such as to the availability
/// list) back to the record before it is saved, and checks the hours make sense.
/// </summary>
internal sealed class WorkforceStaffCatalogHandler : CatalogEntryHandlerBase<StaffMember>
{
    internal readonly IStringLocalizer S;

    public WorkforceStaffCatalogHandler(IStringLocalizer<WorkforceStaffCatalogHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task CreatingAsync(CreatingContext<StaffMember> context, CancellationToken cancellationToken = default)
    {
        context.Model.FlushWorkforceSettings();

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<StaffMember> context, CancellationToken cancellationToken = default)
    {
        context.Model.FlushWorkforceSettings();

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<StaffMember> context, CancellationToken cancellationToken = default)
    {
        var staff = context.Model;

        staff.FlushWorkforceSettings();

        if (staff.MinHoursPerWeek is { } min && staff.MaxHoursPerWeek is { } max && min > max)
        {
            context.Result.Fail(new ValidationResult(S["The fewest hours a week are more than the most."], [nameof(WorkforceStaffSettings.MinHoursPerWeek)]));
        }

        return Task.CompletedTask;
    }
}
