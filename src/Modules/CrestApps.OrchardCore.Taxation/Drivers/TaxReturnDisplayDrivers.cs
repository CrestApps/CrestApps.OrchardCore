using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// The rows of the tax return periods: the registration and period, the return's status, due date and amounts, and
/// the button that prepares or opens it.
/// </summary>
internal sealed class TaxReturnPeriodSummaryDisplayDriver : DisplayDriver<TaxReturnPeriodSummary>
{
    private readonly ITaxReturnService _service;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TaxReturnPeriodSummaryDisplayDriver(
        ITaxReturnService service,
        IStringLocalizerFactory localizerFactory,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _service = service;
        _localizerFactory = localizerFactory;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(TaxReturnPeriodSummary summary, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        var formName = (await _service.GetFormsAsync(includeDisabled: true)).TryGetValue(summary.Form ?? string.Empty, out var form)
            ? _localizerFactory.Localize(form.DisplayName).Value
            : summary.Form;
        var canPrepare = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.ManageTaxReturns);

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{summary.Registration?.Name} {summary.Period?.Key} {formName}".ToLowerInvariant();
        }

        void Fill(TaxReturnPeriodRowViewModel model)
        {
            model.Summary = summary;
            model.FormName = formName;
            model.CanPrepare = canPrepare;
        }

        return Combine(
            Initialize<TaxReturnPeriodRowViewModel>("TaxReturnPeriodSummary_Fields_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<TaxReturnPeriodRowViewModel>("TaxReturnPeriodSummary_Tags_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            Initialize<TaxReturnPeriodRowViewModel>("TaxReturnPeriodSummary_Buttons_SummaryAdmin", Fill).Location("Actions:5"));
    }
}

/// <summary>
/// The cards of a tax return's page: its summary, the figures the modules gave, its form's boxes and its payments.
/// </summary>
internal sealed class TaxReturnDisplayDriver : DisplayDriver<TaxReturn>
{
    /// <summary>
    /// The display type of a return's page.
    /// </summary>
    public const string DetailAdmin = "DetailAdmin";

    private readonly ITaxReturnService _service;
    private readonly ITaxReturnManager _returns;
    private readonly IMerchantTaxRegistrationStore _registrations;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;
    private readonly IDisplayManager<TaxReturnPaymentEntry> _paymentDisplayManager;

    public TaxReturnDisplayDriver(
        ITaxReturnService service,
        ITaxReturnManager returns,
        IMerchantTaxRegistrationStore registrations,
        IStringLocalizerFactory localizerFactory,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        IDisplayManager<TaxReturnPaymentEntry> paymentDisplayManager)
    {
        _service = service;
        _returns = returns;
        _registrations = registrations;
        _localizerFactory = localizerFactory;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
        _paymentDisplayManager = paymentDisplayManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(TaxReturn taxReturn, BuildDisplayContext context)
    {
        if (context.DisplayType != DetailAdmin)
        {
            return null;
        }

        var registration = await _registrations.FindByIdAsync(taxReturn.RegistrationId);
        var formName = (await _service.GetFormsAsync(includeDisabled: true)).TryGetValue(taxReturn.Form ?? string.Empty, out var form)
            ? _localizerFactory.Localize(form.DisplayName).Value
            : taxReturn.Form;
        var amends = string.IsNullOrEmpty(taxReturn.AmendsId) ? null : await _returns.FindByIdAsync(taxReturn.AmendsId);
        var canFile = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.FileTaxReturns);
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        void Fill(TaxReturnDetailViewModel model)
        {
            model.Return = taxReturn;
            model.RegistrationName = registration?.Name;
            model.FormName = formName;
            model.Amends = amends;
            model.Today = today;
            model.CanFile = canFile;
        }

        return Combine(
            Initialize<TaxReturnDetailViewModel>("TaxReturnSummary_DetailAdmin", Fill).Location(DetailAdmin, "Content:1"),
            Initialize<TaxReturnDetailViewModel>("TaxReturnLines_DetailAdmin", Fill).Location(DetailAdmin, "Content:5"),
            Initialize<TaxReturnDetailViewModel>("TaxReturnBoxes_DetailAdmin", Fill).Location(DetailAdmin, "Content:10"),
            Initialize<TaxReturnDetailViewModel>("TaxReturnPayments_DetailAdmin", async model =>
            {
                Fill(model);

                // The payment form is built from drivers, so a pack adds the fields its observer needs.
                if (canFile && taxReturn.Status != TaxReturnStatus.Amended)
                {
                    model.PaymentEditor = await _paymentDisplayManager.BuildEditorAsync(new TaxReturnPaymentEntry { Return = taxReturn }, context.Updater, isNew: true);
                }
            }).Location(DetailAdmin, "Content:15"));
    }
}

/// <summary>
/// With Tax E-Filing: the e-filing card of a tax return's page, with its submissions and the form that files it.
/// </summary>
internal sealed class TaxReturnEFilingDisplayDriver : DisplayDriver<TaxReturn>
{
    private readonly IEFilingService _eFiling;
    private readonly IEFileSubmissionManager _submissions;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;
    private readonly IDisplayManager<EFileSubmissionRequest> _requestDisplayManager;

    public TaxReturnEFilingDisplayDriver(
        IEFilingService eFiling,
        IEFileSubmissionManager submissions,
        IStringLocalizerFactory localizerFactory,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        IDisplayManager<EFileSubmissionRequest> requestDisplayManager)
    {
        _eFiling = eFiling;
        _submissions = submissions;
        _localizerFactory = localizerFactory;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
        _requestDisplayManager = requestDisplayManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(TaxReturn taxReturn, BuildDisplayContext context)
    {
        if (context.DisplayType != TaxReturnDisplayDriver.DetailAdmin)
        {
            return null;
        }

        var target = await _eFiling.FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, taxReturn.ItemId);
        var submissions = await _submissions.ListAsync(TaxationConstants.TaxReturnEFileTargetType, taxReturn.ItemId);

        if (target is null)
        {
            return null;
        }

        var options = _eFiling.GetOptions(target);
        var transportNames = options
            .Select(option => option.Transport)
            .DistinctBy(transport => transport.Name)
            .ToDictionary(transport => transport.Name, transport => _localizerFactory.Localize(transport.DisplayName).Value);
        var canFile = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.FileTaxReturns);
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        return Initialize<TaxReturnEFilingViewModel>("TaxReturnEFiling_DetailAdmin", async model =>
        {
            // The submit form is built from drivers, so a pack adds the choices its format or channel needs.
            if (canFile && string.IsNullOrEmpty(target.NotFileableReason) && options.Count > 0)
            {
                model.SubmitEditor = await _requestDisplayManager.BuildEditorAsync(new EFileSubmissionRequest { Target = target }, context.Updater, isNew: true);
            }

            model.Return = taxReturn;
            model.Submissions = submissions;
            model.NotFileableReason = target.NotFileableReason;
            model.TransportNames = transportNames;
            model.Today = today;
            model.CanFile = canFile;
            model.Options =
            [
                .. options.Select(option => new SelectListItem(
                    $"{_localizerFactory.Localize(option.Format.DisplayName).Value} · {_localizerFactory.Localize(option.Transport.DisplayName).Value}",
                    $"{option.Format.Name}|{option.Transport.Name}")),
            ];
        }).Location(TaxReturnDisplayDriver.DetailAdmin, "Content:20");
    }
}
