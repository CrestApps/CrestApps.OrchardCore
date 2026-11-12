using CrestApps.Core;
using CrestApps.OrchardCore.Skills.Core.Drivers;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Staff.Drivers;

/// <summary>
/// The Skills card of the staff editor. A staff member and their user are one skill holder.
/// </summary>
internal sealed class StaffSkillsDisplayDriver : SkillSubjectSkillsDisplayDriverBase<StaffMember>
{
    public StaffSkillsDisplayDriver(
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(skillManager, assignmentService, httpContextAccessor, authorizationService)
    {
    }

    protected override string ShapeType => "StaffMemberSkills_Edit";

    protected override SkillSubject GetSubject(StaffMember model, bool isNew)
        => SkillSubject.Staff(model.ItemId);
}

public sealed class StaffPayViewModel
{
    public decimal? AnnualSalary { get; set; }

    public decimal? HourlyCost { get; set; }

    public IList<StaffPayRateRowViewModel> Rates { get; set; } = [];
}

public sealed class StaffPayRateRowViewModel
{
    public DateOnly? EffectiveFrom { get; set; }

    public decimal? HourlyRate { get; set; }

    public string ActivityCodeId { get; set; }
}

/// <summary>
/// The Pay card of the staff editor, for people allowed to manage pay.
/// </summary>
internal sealed class StaffPayDisplayDriver : DisplayDriver<StaffMember>
{
    private const int EmptyRows = 2;

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public StaffPayDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<StaffPayDisplayDriver> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(StaffMember staff, BuildEditorContext context)
    {
        if (!await CanManagePayAsync())
        {
            return null;
        }

        var pay = staff.TryGet<StaffPayRates>(out var rates) ? rates : new StaffPayRates();

        return Editor(new StaffPayViewModel
        {
            AnnualSalary = pay.AnnualSalary,
            HourlyCost = pay.HourlyCost,
            Rates = pay.Rates.OrderBy(rate => rate.EffectiveFrom).Select(rate => new StaffPayRateRowViewModel
            {
                EffectiveFrom = rate.EffectiveFrom,
                HourlyRate = rate.HourlyRate,
                ActivityCodeId = rate.ActivityCodeId,
            }).ToList(),
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(StaffMember staff, UpdateEditorContext context)
    {
        if (!await CanManagePayAsync())
        {
            return null;
        }

        var model = new StaffPayViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var rows = (model.Rates ?? []).Where(row => row.EffectiveFrom is not null || row.HourlyRate is not null).ToList();

        if (rows.Any(row => row.EffectiveFrom is null || row.HourlyRate is null or < 0))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Each pay rate needs a start date and a rate of zero or more."]);
        }

        if (model.AnnualSalary is < 0 || model.HourlyCost is < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Pay can't be negative."]);
        }

        staff.Put(new StaffPayRates
        {
            AnnualSalary = model.AnnualSalary,
            HourlyCost = model.HourlyCost,
            Rates = rows
                .Where(row => row.EffectiveFrom is not null && row.HourlyRate is >= 0)
                .Select(row => new StaffPayRate
                {
                    EffectiveFrom = row.EffectiveFrom.Value,
                    HourlyRate = row.HourlyRate.Value,
                    ActivityCodeId = string.IsNullOrWhiteSpace(row.ActivityCodeId) ? null : row.ActivityCodeId.Trim(),
                })
                .ToList(),
        });

        return Editor(model);
    }

    private ShapeResult Editor(StaffPayViewModel values)
    {
        var rates = (values.Rates ?? []).ToList();

        while (rates.Count(row => row.EffectiveFrom is null && row.HourlyRate is null) < EmptyRows)
        {
            rates.Add(new StaffPayRateRowViewModel());
        }

        return Initialize<StaffPayViewModel>("StaffMemberPay_Edit", model =>
        {
            model.AnnualSalary = values.AnnualSalary;
            model.HourlyCost = values.HourlyCost;
            model.Rates = rates;
        }).Location("Content:20");
    }

    private async Task<bool> CanManagePayAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        return principal is not null && await _authorizationService.AuthorizeAsync(principal, StaffPermissions.ManageStaffPay);
    }
}

public sealed class StaffBadgeViewModel
{
    public string Pin { get; set; }

    public bool ClearPin { get; set; }

    public bool HasPin { get; set; }
}

/// <summary>
/// The Badge card of the staff editor: sets or removes the PIN used with the employee number at kiosks and registers.
/// </summary>
internal sealed class StaffBadgeDisplayDriver : DisplayDriver<StaffMember>
{
    private readonly IStaffBadgeService _badgeService;

    public StaffBadgeDisplayDriver(IStaffBadgeService badgeService)
    {
        _badgeService = badgeService;
    }

    public override IDisplayResult Edit(StaffMember staff, BuildEditorContext context)
        => Initialize<StaffBadgeViewModel>("StaffMemberBadge_Edit", model => model.HasPin = _badgeService.HasPin(staff)).Location("Content:21");

    public override async Task<IDisplayResult> UpdateAsync(StaffMember staff, UpdateEditorContext context)
    {
        var model = new StaffBadgeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.ClearPin)
        {
            _badgeService.ClearPin(staff);
        }
        else if (!string.IsNullOrWhiteSpace(model.Pin) && _badgeService.SetPin(staff, model.Pin) is { } error)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(StaffBadgeViewModel.Pin), error);
        }

        return Edit(staff, context);
    }
}
