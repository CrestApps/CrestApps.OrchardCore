using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Skills.Core.Drivers;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

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

public class StaffPayViewModel
{
    public string PayBasis { get; set; }

    public string Currency { get; set; }

    public string HourlyCost { get; set; }

    public string CommissionRate { get; set; }

    public IList<StaffPayRateRowViewModel> Rates { get; set; } = [];

    public IList<StaffSalaryRowViewModel> Salaries { get; set; } = [];
}

public class StaffPayRateRowViewModel
{
    public DateOnly? EffectiveFrom { get; set; }

    public string HourlyRate { get; set; }

    public string ActivityCodeId { get; set; }
}

public class StaffSalaryRowViewModel
{
    public DateOnly? EffectiveFrom { get; set; }

    public string AnnualAmount { get; set; }
}

/// <summary>
/// The Pay card of the staff editor, for people allowed to manage pay: the only place pay is edited. Amounts are
/// typed and shown with the invariant culture, so a decimal comma never changes a rate.
/// </summary>
internal sealed class StaffPayDisplayDriver : DisplayDriver<StaffMember>
{
    private const int EmptyRows = 2;

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public StaffPayDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        IStringLocalizer<StaffPayDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(StaffMember staff, BuildDisplayContext context)
    {
        if (context.DisplayType != StaffProfileSections.DisplayType || !await CanManagePayAsync())
        {
            return null;
        }

        // Pay is private: until the viewer confirms their password, the card is the one prompt, or nothing.
        switch (await StaffPayPrivacy.DecideAsync(_httpContextAccessor.HttpContext))
        {
            case StaffPayVisibility.Hide:
                return null;
            case StaffPayVisibility.Prompt:
                return Dynamic(StaffPayPrivacy.PromptShape, shape => { shape.ConfirmUrl = StaffPayPrivacy.ConfirmUrl(_httpContextAccessor.HttpContext); })
                    .Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Pay, "0"));
        }

        var pay = staff.TryGet<StaffPayRates>(out var rates) ? rates : new StaffPayRates();
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        return Initialize<StaffPayProfileViewModel>("StaffProfilePay", model =>
        {
            model.PayBasis = pay.PayBasis;
            model.Currency = pay.Currency;
            model.CurrentRate = pay.RateOn(today);
            model.AnnualSalary = pay.SalaryOn(today);
            model.HourlyCost = pay.HourlyCost;
            model.CommissionRate = pay.CommissionRate;
            model.Rates = [.. pay.Rates.OrderByDescending(rate => rate.EffectiveFrom).Where(rate => rate.ActivityCodeId is null).Select(rate => new StaffPayRateRowViewModel
            {
                EffectiveFrom = rate.EffectiveFrom,
                HourlyRate = Text(rate.HourlyRate),
            })];
            model.Salaries = [.. pay.Salaries.OrderByDescending(salary => salary.EffectiveFrom).Select(salary => new StaffSalaryRowViewModel
            {
                EffectiveFrom = salary.EffectiveFrom,
                AnnualAmount = Text(salary.AnnualAmount),
            })];
        }).Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Pay, "1"));
    }

    public override async Task<IDisplayResult> EditAsync(StaffMember staff, BuildEditorContext context)
    {
        if (!await CanManagePayAsync())
        {
            return null;
        }

        // Pay rates are private: until the editor confirms their password, the card is the prompt and saving leaves them.
        if (await StaffPayPrivacy.DecideAsync(_httpContextAccessor.HttpContext) != StaffPayVisibility.Show)
        {
            return Dynamic(StaffPayPrivacy.PromptShape, shape => { shape.ConfirmUrl = StaffPayPrivacy.ConfirmUrl(_httpContextAccessor.HttpContext); });
        }

        var pay = staff.TryGet<StaffPayRates>(out var rates) ? rates : new StaffPayRates();
        var salaries = pay.Salaries.OrderBy(salary => salary.EffectiveFrom).Select(salary => new StaffSalaryRowViewModel
        {
            EffectiveFrom = salary.EffectiveFrom,
            AnnualAmount = Text(salary.AnnualAmount),
        }).ToList();

        // A salary kept before salaries were dated shows as a row without a start date, to be dated when next saved.
        if (salaries.Count == 0 && pay.AnnualSalary is { } legacy)
        {
            salaries.Add(new StaffSalaryRowViewModel { AnnualAmount = Text(legacy) });
        }

        return Editor(new StaffPayViewModel
        {
            PayBasis = pay.PayBasis.ToString(),
            Currency = pay.Currency,
            HourlyCost = Text(pay.HourlyCost),
            CommissionRate = Text(pay.CommissionRate),
            Rates = pay.Rates.OrderBy(rate => rate.EffectiveFrom).Select(rate => new StaffPayRateRowViewModel
            {
                EffectiveFrom = rate.EffectiveFrom,
                HourlyRate = Text(rate.HourlyRate),
                ActivityCodeId = rate.ActivityCodeId,
            }).ToList(),
            Salaries = salaries,
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(StaffMember staff, UpdateEditorContext context)
    {
        if (!await CanManagePayAsync())
        {
            return null;
        }

        if (_httpContextAccessor.HttpContext?.RequestServices.GetService<CrestApps.OrchardCore.Users.IPasswordConfirmation>() is not { } confirmation
            || !await confirmation.IsConfirmedAsync(_httpContextAccessor.HttpContext.User))
        {
            return await EditAsync(staff, context);
        }

        var model = new StaffPayViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var rates = new List<StaffPayRate>();
        var salaries = new List<StaffSalary>();
        var badRate = false;
        var badSalary = false;

        foreach (var row in (model.Rates ?? []).Where(row => row.EffectiveFrom is not null || !string.IsNullOrWhiteSpace(row.HourlyRate)))
        {
            if (row.EffectiveFrom is not { } from || Parse(row.HourlyRate) is not { } rate || rate < 0)
            {
                badRate = true;

                continue;
            }

            rates.Add(new StaffPayRate
            {
                EffectiveFrom = from,
                HourlyRate = rate,
                ActivityCodeId = string.IsNullOrWhiteSpace(row.ActivityCodeId) ? null : row.ActivityCodeId.Trim(),
            });
        }

        foreach (var row in (model.Salaries ?? []).Where(row => row.EffectiveFrom is not null || !string.IsNullOrWhiteSpace(row.AnnualAmount)))
        {
            if (row.EffectiveFrom is not { } from || Parse(row.AnnualAmount) is not { } amount || amount < 0)
            {
                badSalary = true;

                continue;
            }

            salaries.Add(new StaffSalary { EffectiveFrom = from, AnnualAmount = amount });
        }

        if (badRate)
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Each pay rate needs a start date and a rate of zero or more, such as 25.50."]);
        }

        if (badSalary)
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Each salary needs a start date and a yearly amount of zero or more, such as 52000."]);
        }

        if (rates.GroupBy(rate => (rate.EffectiveFrom, rate.ActivityCodeId)).Any(group => group.Count() > 1) || salaries.GroupBy(salary => salary.EffectiveFrom).Any(group => group.Count() > 1))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Two rows start on the same day. Keep one."]);
        }

        var cost = Parse(model.HourlyCost);

        if (!string.IsNullOrWhiteSpace(model.HourlyCost) && cost is not >= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.HourlyCost), S["Enter the cost of an hour as a number of zero or more, such as 38.20."]);
        }

        var commission = Parse(model.CommissionRate);

        if (!string.IsNullOrWhiteSpace(model.CommissionRate) && commission is not (>= 0 and <= 100))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CommissionRate), S["Enter the commission as a percent from 0 to 100, such as 5 for 5%."]);
        }

        var currency = string.IsNullOrWhiteSpace(model.Currency) ? null : model.Currency.Trim().ToUpperInvariant();

        if (currency is not null && (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Currency), S["Enter a three-letter currency code, such as USD, or leave it empty for the site's currency."]);
        }

        if (!Enum.TryParse<StaffPayBasis>(model.PayBasis, out var basis))
        {
            basis = StaffPayBasis.Hourly;
        }

        if (context.Updater.ModelState.IsValid)
        {
            staff.Put(new StaffPayRates
            {
                PayBasis = basis,
                Currency = currency,
                HourlyCost = cost,
                CommissionRate = commission,
                Rates = rates,
                Salaries = salaries,

                // The undated salary is dated now or dropped: the rows are the record.
                AnnualSalary = null,
            });
        }

        return Editor(model);
    }

    private ShapeResult Editor(StaffPayViewModel values)
    {
        var rates = (values.Rates ?? []).ToList();
        var salaries = (values.Salaries ?? []).ToList();

        while (rates.Count(row => row.EffectiveFrom is null && string.IsNullOrWhiteSpace(row.HourlyRate)) < EmptyRows)
        {
            rates.Add(new StaffPayRateRowViewModel());
        }

        if (!salaries.Any(row => row.EffectiveFrom is null && string.IsNullOrWhiteSpace(row.AnnualAmount)))
        {
            salaries.Add(new StaffSalaryRowViewModel());
        }

        return Initialize<StaffPayViewModel>("StaffMemberPay_Edit", model =>
        {
            model.PayBasis = values.PayBasis;
            model.Currency = values.Currency;
            model.HourlyCost = values.HourlyCost;
            model.CommissionRate = values.CommissionRate;
            model.Rates = rates;
            model.Salaries = salaries;
        }).Location("Content:20");
    }

    private async Task<bool> CanManagePayAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        return principal is not null && await _authorizationService.AuthorizeAsync(principal, StaffPermissions.ManageStaffPay);
    }

    internal static string Text(decimal? value)
        => value?.ToString("0.####", CultureInfo.InvariantCulture);

    internal static decimal? Parse(string value)
        => !string.IsNullOrWhiteSpace(value) && decimal.TryParse(value.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? Math.Round(number, 4, MidpointRounding.AwayFromZero)
            : null;
}

public class StaffBadgeViewModel
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
