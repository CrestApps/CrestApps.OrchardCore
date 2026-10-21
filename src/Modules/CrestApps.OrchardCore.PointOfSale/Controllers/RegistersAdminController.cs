using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.PointOfSale.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.PointOfSale.Controllers;

/// <summary>
/// Commerce › Point of sale: the registers at each store, and the shifts worked at them with their cash counts.
/// </summary>
[Admin("point-of-sale/{action}/{itemId?}", "PointOfSale{action}")]
public sealed class RegistersAdminController : Controller
{
    private const string _optionsRegister = "Options.RegisterId";

    private readonly IRegisterStore _registers;
    private readonly IRegisterShiftStore _shifts;
    private readonly PosShiftService _shiftService;
    private readonly IInventoryLocationService _locations;
    private readonly IRoleService _roleService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public RegistersAdminController(
        IRegisterStore registers,
        IRegisterShiftStore shifts,
        PosShiftService shiftService,
        IInventoryLocationService locations,
        IRoleService roleService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<RegistersAdminController> htmlLocalizer,
        IStringLocalizer<RegistersAdminController> stringLocalizer)
    {
        _registers = registers;
        _shifts = shifts;
        _shiftService = shiftService;
        _locations = locations;
        _roleService = roleService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("point-of-sale/registers", "PointOfSaleRegisters")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, registers) = await _registers.PageAsync(pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var pagerShape = await shapeFactory.PagerAsync(pager, count, new RouteData());

        return View(new RegistersIndexViewModel
        {
            List = new AdminListContext<Register>
            {
                Source = registers,
                Pager = (IShape)pagerShape,
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        return View("Edit", await PopulateAsync(new RegisterEditViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(RegisterEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Edit", await PopulateAsync(model));
        }

        var register = new Register { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow };

        Apply(model, register);
        await _registers.CreateAsync(register);
        await _notifier.SuccessAsync(H["The register was added. Staff who may sell at registers can open a shift at it from the till app."]);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        if (await _registers.FindByIdAsync(itemId) is not { } register)
        {
            return NotFound();
        }

        return View(await PopulateAsync(new RegisterEditViewModel
        {
            Name = register.Name,
            LocationId = register.LocationId,
            Currency = register.Currency,
            IsEnabled = register.IsEnabled,
            Roles = [.. register.Roles ?? []],
            AddressLine1 = register.Address?.AddressLine1,
            AddressLine2 = register.Address?.AddressLine2,
            City = register.Address?.City,
            Region = register.Address?.Region,
            PostalCode = register.Address?.PostalCode,
            Country = register.Address?.Country,
        }, register.ItemId));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, RegisterEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        if (await _registers.FindByIdAsync(itemId) is not { } register)
        {
            return NotFound();
        }

        await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            return View(await PopulateAsync(model, register.ItemId));
        }

        Apply(model, register);
        await _registers.UpdateAsync(register);
        await _notifier.SuccessAsync(H["The register was saved."]);

        return RedirectToAction(nameof(Index));
    }

    [Admin("point-of-sale/shifts", "PointOfSaleShifts")]
    public async Task<IActionResult> Shifts(RegisterShiftsOptions options, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        options ??= new RegisterShiftsOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, shifts) = await _shifts.PageAsync(options.RegisterId, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.RegisterId))
        {
            routeData.Values.TryAdd(_optionsRegister, options.RegisterId);
        }

        options.Registers =
        [
            new SelectListItem(S["Every register"], string.Empty, string.IsNullOrEmpty(options.RegisterId)),
            .. (await _registers.GetAllAsync()).OrderBy(register => register.Name, StringComparer.OrdinalIgnoreCase)
                .Select(register => new SelectListItem(register.Name, register.ItemId, register.ItemId == options.RegisterId)),
        ];

        var pagerShape = await shapeFactory.PagerAsync(pager, count, routeData);

        return View(new RegisterShiftsViewModel
        {
            Options = options,
            List = new AdminListContext<RegisterShift>
            {
                Source = shifts,
                Pager = (IShape)pagerShape,
                ToolbarActions = await shapeFactory.CreateAsync("RegisterShiftsListFilters", Arguments.From(new { Options = options })),
            },
        });
    }

    [HttpPost]
    [ActionName(nameof(Shifts))]
    [FormValueRequired("submit.Filter")]
    [Admin("point-of-sale/shifts", "PointOfSaleShifts")]
    public async Task<IActionResult> ShiftsFilterPost(RegisterShiftsOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options?.RegisterId))
        {
            routeValues.TryAdd(_optionsRegister, options.RegisterId);
        }

        return RedirectToAction(nameof(Shifts), routeValues);
    }

    public async Task<IActionResult> Shift(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        if (await _shifts.FindByIdAsync(itemId) is not { } shift)
        {
            return NotFound();
        }

        var summary = shift.Summary ?? await _shiftService.SummarizeAsync(shift, HttpContext.RequestAborted);

        return View(new RegisterShiftDetailViewModel
        {
            Shift = shift,
            Summary = summary,
            ExpectedCash = shift.ExpectedCash ?? shift.GetExpectedCash(summary.Cash),
        });
    }

    private async Task ValidateAsync(RegisterEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Name the register, such as the store and till it's at."]);
        }

        var locations = await _locations.GetLocationsAsync();

        if (!locations.Any(location => location.LocationId == model.LocationId))
        {
            ModelState.AddModelError(nameof(model.LocationId), S["Choose the store location the register sells from."]);
        }

        if (string.IsNullOrWhiteSpace(model.AddressLine1) || string.IsNullOrWhiteSpace(model.City) || !CountryCodeUtility.IsCountryCode(model.Country))
        {
            ModelState.AddModelError(nameof(model.AddressLine1), S["Enter the store's street address, city and two-letter country code: its sales are taxed by it."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Currency) && model.Currency.Trim().Length != 3)
        {
            ModelState.AddModelError(nameof(model.Currency), S["Enter a three-letter currency code, such as USD, or leave it blank."]);
        }
    }

    private void Apply(RegisterEditViewModel model, Register register)
    {
        register.Name = model.Name.Trim();
        register.LocationId = model.LocationId;
        register.Currency = string.IsNullOrWhiteSpace(model.Currency) ? null : model.Currency.Trim().ToUpperInvariant();
        register.IsEnabled = model.IsEnabled;
        register.Roles = [.. (model.Roles ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Distinct(StringComparer.OrdinalIgnoreCase)];
        register.Address = new Address
        {
            Name = model.Name.Trim(),
            AddressLine1 = model.AddressLine1?.Trim(),
            AddressLine2 = string.IsNullOrWhiteSpace(model.AddressLine2) ? null : model.AddressLine2.Trim(),
            City = model.City?.Trim(),
            Region = string.IsNullOrWhiteSpace(model.Region) ? null : model.Region.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(model.PostalCode) ? null : model.PostalCode.Trim(),
            Country = model.Country?.Trim().ToUpperInvariant(),
        };
        register.ModifiedUtc = _clock.UtcNow;
    }

    private async Task<RegisterEditViewModel> PopulateAsync(RegisterEditViewModel model, string itemId = null)
    {
        model.ItemId = itemId;
        model.Locations = [.. (await _locations.GetLocationsAsync()).Select(location => new SelectListItem(location.Name, location.LocationId, location.LocationId == model.LocationId))];
        model.AvailableRoles = [.. (await _roleService.GetRoleNamesAsync()).Where(role => !string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase) && !string.Equals(role, "Authenticated", StringComparison.OrdinalIgnoreCase))];

        return model;
    }
}
