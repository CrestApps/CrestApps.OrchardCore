using System.Globalization;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows and the editor of garnishment orders: the person (a pay profile of an employer the user manages
/// garnishments for), the order, the limits its kind gives, the payee and how it is paid, and what it withheld.
/// </summary>
internal sealed class GarnishmentOrderDisplayDriver : DisplayDriver<GarnishmentOrder>
{
    private readonly IPayProfileManager _profiles;
    private readonly IPayCodeManager _codes;
    private readonly IPayrollScopeService _scope;
    private readonly PayrollNames _names;
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public GarnishmentOrderDisplayDriver(
        IPayProfileManager profiles,
        IPayCodeManager codes,
        IPayrollScopeService scope,
        PayrollNames names,
        IAddressEditorService addressEditor,
        IShapeFactory shapeFactory,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        IServiceProvider services,
        IStringLocalizer<GarnishmentOrderDisplayDriver> stringLocalizer)
    {
        _profiles = profiles;
        _codes = codes;
        _scope = scope;
        _names = names;
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
        _services = services;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(GarnishmentOrder order, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        var name = await _names.StaffAsync(order.StaffMemberId);

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{name} {order.CaseNumber} {order.Agency} {order.PayeeName}".ToLowerInvariant();
        }

        var employer = await _names.EmployerAsync(order.EmployerId);

        return Combine(
            Initialize<GarnishmentRowViewModel>("GarnishmentOrder_Fields_SummaryAdmin", model =>
            {
                model.Order = order;
                model.StaffName = name;
                model.EmployerName = employer;
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("GarnishmentOrder_Tags_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("GarnishmentOrder_Buttons_SummaryAdmin", order).Location("Actions:5"),
            View("GarnishmentOrder_ActionsMenu_SummaryAdmin", order).Location("ActionsMenu:10"));
    }

    public override async Task<IDisplayResult> EditAsync(GarnishmentOrder order, BuildEditorContext context)
    {
        if (context.IsNew)
        {
            var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

            if (order.ReceivedDate == default)
            {
                order.ReceivedDate = today;
            }

            if (order.StartDate == default)
            {
                order.StartDate = today;
            }
        }

        return Combine(
            Initialize<GarnishmentOrderViewModel>("GarnishmentOrderFields_Edit", async model =>
            {
                Fill(model, order);
                model.IsNew = context.IsNew;
                await OptionsAsync(model, order);
            }).Location("Content:1"),
            await AddressAsync(await _addressEditor.BuildAsync(order.PayeeAddress, AddressOptions(order.PayeeAddress?.Country))));
    }

    public override async Task<IDisplayResult> UpdateAsync(GarnishmentOrder order, UpdateEditorContext context)
    {
        var model = new GarnishmentOrderViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            order.ProfileId = model.ProfileId;
        }

        var type = Enum.TryParse<GarnishmentType>(model.Type, out var parsedType) ? parsedType : GarnishmentType.Other;
        var typeChanged = context.IsNew || type != order.Type;

        order.Type = type;
        order.CaseNumber = model.CaseNumber?.Trim();
        order.Agency = model.Agency?.Trim();
        order.PayCodeId = model.PayCodeId;
        order.Amount = Number(model.Amount, nameof(model.Amount), context);
        order.Percent = Number(model.Percent, nameof(model.Percent), context);
        order.LimitPercent = Number(model.LimitPercent, nameof(model.LimitPercent), context);
        order.ProtectedWeeklyAmount = Number(model.ProtectedWeeklyAmount, nameof(model.ProtectedWeeklyAmount), context);
        order.ExemptAmount = Number(model.ExemptAmount, nameof(model.ExemptAmount), context);
        order.TotalOwed = Number(model.TotalOwed, nameof(model.TotalOwed), context);
        order.Priority = int.TryParse(model.Priority, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority) ? priority : 0;
        order.ReceivedDate = model.ReceivedDate ?? order.ReceivedDate;
        order.EndDate = model.EndDate;
        order.Status = Enum.TryParse<GarnishmentStatus>(model.Status, out var status) ? status : GarnishmentStatus.Active;
        order.PayeeName = model.PayeeName?.Trim();
        order.PaymentMethod = Enum.TryParse<GarnishmentPaymentMethod>(model.PaymentMethod, out var method) ? method : GarnishmentPaymentMethod.Check;
        order.PayeeBankAccountId = order.PaymentMethod == GarnishmentPaymentMethod.BankAccount && !string.IsNullOrEmpty(model.PayeeBankAccountId) ? model.PayeeBankAccountId : null;
        order.Notes = model.Notes;

        if (model.StartDate is { } start)
        {
            order.StartDate = start;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.StartDate), S["Give the first pay date the order applies to."]);
        }

        // A new kind brings its own limits where the form left them empty.
        if (typeChanged)
        {
            GarnishmentOrderHandler.Defaults(order);
        }

        var address = new GarnishmentPayeeAddressViewModel();

        await context.Updater.TryUpdateModelAsync(address, Prefix + "Payee");
        address.Address ??= new AddressEditorViewModel();

        if (!address.Address.HasAnyValue())
        {
            order.PayeeAddress = null;
        }
        else
        {
            var result = await _addressEditor.ValidateAsync(address.Address, context.Updater.ModelState, $"{Prefix}Payee.{nameof(address.Address)}", AddressOptions(address.Address.Country));

            if (result.IsValid)
            {
                order.PayeeAddress = result.Address;
            }
        }

        if (order.PaymentMethod == GarnishmentPaymentMethod.Check && order.PayeeAddress is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PaymentMethod), S["Enter the payee's mailing address to pay by check."]);
        }

        await _addressEditor.PopulateAsync(address.Address, AddressOptions(address.Address.Country));

        return Combine(
            Initialize<GarnishmentOrderViewModel>("GarnishmentOrderFields_Edit", async edit =>
            {
                Fill(edit, order);
                edit.IsNew = context.IsNew;
                await OptionsAsync(edit, order);
            }).Location("Content:1"),
            await AddressAsync(address.Address));
    }

    private static void Fill(GarnishmentOrderViewModel model, GarnishmentOrder order)
    {
        model.ProfileId = order.ProfileId;
        model.Type = order.Type.ToString();
        model.CaseNumber = order.CaseNumber;
        model.Agency = order.Agency;
        model.PayCodeId = order.PayCodeId;
        model.Amount = Text(order.Amount);
        model.Percent = Text(order.Percent);
        model.LimitPercent = Text(order.LimitPercent);
        model.ProtectedWeeklyAmount = Text(order.ProtectedWeeklyAmount);
        model.ExemptAmount = Text(order.ExemptAmount);
        model.TotalOwed = Text(order.TotalOwed);
        model.Priority = order.Priority == 0 ? null : order.Priority.ToString(CultureInfo.InvariantCulture);
        model.ReceivedDate = order.ReceivedDate == default ? null : order.ReceivedDate;
        model.StartDate = order.StartDate == default ? null : order.StartDate;
        model.EndDate = order.EndDate;
        model.Status = order.Status.ToString();
        model.PayeeName = order.PayeeName;
        model.PaymentMethod = order.PaymentMethod.ToString();
        model.PayeeBankAccountId = order.PayeeBankAccountId;
        model.Notes = order.Notes;
        model.Withheld = order.Withheld;
        model.Remaining = order.Remaining;
        model.Remittances = [.. order.Remittances.OrderByDescending(remittance => remittance.PayDate)];
    }

    private async Task OptionsAsync(GarnishmentOrderViewModel model, GarnishmentOrder order)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var scope = await _scope.GetScopeAsync(user, PayrollPermissions.ManageGarnishments);

        if (string.IsNullOrEmpty(order.ItemId) || model.IsNew)
        {
            var profiles = await _profiles.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds]);

            foreach (var profile in profiles)
            {
                var label = $"{await _names.StaffAsync(profile.StaffMemberId)} ({await _names.EmployerAsync(profile.EmployerId)})";

                model.Profiles.Add(new SelectListItem(label, profile.ItemId, profile.ItemId == order.ProfileId));
            }

            model.Profiles = [.. model.Profiles.OrderBy(option => option.Text, StringComparer.CurrentCultureIgnoreCase)];
        }

        if (!string.IsNullOrEmpty(order.ProfileId) && await _profiles.FindByIdAsync(order.ProfileId) is { } chosen)
        {
            model.ProfileName = $"{await _names.StaffAsync(chosen.StaffMemberId)} ({await _names.EmployerAsync(chosen.EmployerId)})";
        }

        model.PayCodes = [.. (await _codes.GetAllAsync())
            .Where(code => code.Class == PayCodeClass.Deduction)
            .OrderBy(code => code.Code, StringComparer.OrdinalIgnoreCase)
            .Select(code => new SelectListItem($"{code.Code} {code.Name}", code.ItemId, code.ItemId == order.PayCodeId))];

        // The payee's own accounts: owned by someone, never a staff member and never one of the business's.
        if (_services.GetService<IBankAccountManager>() is { } accounts)
        {
            model.BankAccounts = [.. (await accounts.GetAllAsync())
                .Where(account => !account.Owner.IsEmpty && account.Owner.Kind != Staff.StaffConstants.PartyKind
                    && (account.Status is not (BankAccountStatus.Closed or BankAccountStatus.Failed) || account.ItemId == order.PayeeBankAccountId))
                .OrderBy(account => account.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(account => new SelectListItem(account.Name, account.ItemId, account.ItemId == order.PayeeBankAccountId))];
        }
    }

    private async Task<ShapeResult> AddressAsync(AddressEditorViewModel address)
    {
        var editor = await _shapeFactory.CreateAddressEditorAsync(address, $"{Prefix}Payee.{nameof(GarnishmentPayeeAddressViewModel.Address)}");

        return Initialize<GarnishmentPayeeAddressViewModel>("GarnishmentPayeeAddress_Edit", model =>
        {
            model.Address = address;
            model.AddressEditor = editor;
        }).Location("Content:3");
    }

    private static AddressEditorOptions AddressOptions(string country)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = string.IsNullOrWhiteSpace(country) ? null : country,
        };

    private decimal? Number(string text, string field, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        context.Updater.ModelState.AddModelError(Prefix, field, S["Type a number of zero or more, with a dot for decimals."]);

        return null;
    }

    private static string Text(decimal? value)
        => value?.ToString("0.##########", CultureInfo.InvariantCulture);
}
