using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using CrestApps.OrchardCore.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// The administrator's screens for installment plans: the list, creating a plan, taking its down payment by card,
/// and managing it afterwards.
/// </summary>
[Admin("installment-plans/{action}/{itemId?}", "InstallmentPlans{action}")]
[Feature(SubscriptionConstants.Features.Installments)]
public sealed class InstallmentPlansController : Controller
{
    private const string _optionsStatus = "Options.Status";
    private const string _optionsSearch = "Options.Search";
    private const int MaxProviderDataEntries = 16;
    private const int MaxProviderDataKeyLength = 64;
    private const int MaxProviderDataValueLength = 512;

    private readonly IInstallmentPlanStore _planStore;
    private readonly IInstallmentPlanService _planService;
    private readonly ICheckoutEngine _checkoutEngine;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly IEnumerable<ICheckoutSavedPaymentMethodProvider> _savedPaymentMethodProviders;
    private readonly ITransactionManager _transactionManager;
    private readonly IProductCurrencyProvider _currencyProvider;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlansController"/> class.
    /// </summary>
    public InstallmentPlansController(
        IInstallmentPlanStore planStore,
        IInstallmentPlanService planService,
        ICheckoutEngine checkoutEngine,
        ICheckoutSessionStore sessionStore,
        ICheckoutPaymentProviderResolver providerResolver,
        IEnumerable<ICheckoutSavedPaymentMethodProvider> savedPaymentMethodProviders,
        ITransactionManager transactionManager,
        IProductCurrencyProvider currencyProvider,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<InstallmentPlansController> htmlLocalizer,
        IStringLocalizer<InstallmentPlansController> stringLocalizer)
    {
        _planStore = planStore;
        _planService = planService;
        _checkoutEngine = checkoutEngine;
        _sessionStore = sessionStore;
        _providerResolver = providerResolver;
        _savedPaymentMethodProviders = savedPaymentMethodProviders;
        _transactionManager = transactionManager;
        _currencyProvider = currencyProvider;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists installment plans.
    /// </summary>
    [Admin("installment-plans", "InstallmentPlansIndex")]
    public async Task<IActionResult> Index(
        InstallmentPlansIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _planStore.PageAsync(pager.Page, pager.PageSize, new InstallmentPlanQuery
        {
            Status = options.Status,
            Search = options.Search,
        });

        var routeData = new RouteData();

        if (options.Status.HasValue)
        {
            routeData.Values.TryAdd(_optionsStatus, options.Status.Value);
        }

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        options.Statuses = BuildStatusItems(options.Status);

        return View(new InstallmentPlansIndexViewModel
        {
            Options = options,
            Plans = [.. result.Entries],
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        });
    }

    /// <summary>
    /// Applies the list filters.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("installment-plans", "InstallmentPlansIndex")]
    public async Task<IActionResult> IndexFilterPost(InstallmentPlansIndexOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (options.Status.HasValue)
        {
            routeValues.TryAdd(_optionsStatus, options.Status.Value);
        }

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeValues.TryAdd(_optionsSearch, options.Search);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Shows the form that creates a plan.
    /// </summary>
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var settings = await _siteService.GetSettingsAsync<InstallmentPlanSettings>();
        var subscriptionSettings = await _siteService.GetSettingsAsync<SubscriptionSettings>();

        var model = new CreateInstallmentPlanViewModel
        {
            Currency = subscriptionSettings.Currency,
            CollectionMethod = settings.DefaultCollectionMethod,
            InstallmentCount = 6,
            Frequency = InstallmentFrequency.Monthly,
            FirstDueDate = _clock.UtcNow.Date.AddMonths(1),
        };

        await PopulateAsync(model);

        if (!GetEligiblePaymentMethods().Any())
        {
            await _notifier.WarningAsync(H["No payment provider that can keep a card for later payments is enabled. Enable and configure one, such as Stripe, before creating a plan."]);
        }

        return View(model);
    }

    /// <summary>
    /// Creates a plan and moves on to taking its down payment.
    /// </summary>
    /// <param name="model">The form.</param>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(CreateInstallmentPlanViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        if (!model.TotalAmount.HasValue)
        {
            ModelState.AddModelError(nameof(model.TotalAmount), S["Enter the total."]);
        }

        if (!model.DownPaymentAmount.HasValue)
        {
            ModelState.AddModelError(nameof(model.DownPaymentAmount), S["Enter the down payment."]);
        }

        if (!model.InstallmentCount.HasValue)
        {
            ModelState.AddModelError(nameof(model.InstallmentCount), S["Enter the number of payments."]);
        }

        if (!model.FirstDueDate.HasValue)
        {
            ModelState.AddModelError(nameof(model.FirstDueDate), S["Choose when the first payment falls due."]);
        }

        if (ModelState.IsValid)
        {
            var result = await _planService.CreateAsync(new CreateInstallmentPlanRequest
            {
                Title = model.Title,
                CustomerUserId = model.NewCustomer ? null : model.CustomerUserId,
                NewCustomerName = model.NewCustomer ? model.NewCustomerName : null,
                NewCustomerEmail = model.NewCustomer ? model.NewCustomerEmail : null,
                Currency = model.Currency,
                TotalAmount = model.TotalAmount.Value,
                DownPaymentAmount = model.DownPaymentAmount.Value,
                InstallmentCount = model.InstallmentCount.Value,
                Frequency = model.Frequency,
                FirstDueUtc = DateTime.SpecifyKind(model.FirstDueDate.Value.Date, DateTimeKind.Utc),
                CollectionMethod = model.CollectionMethod,
                Notes = model.Notes,
            }, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Pay), new { itemId = result.Plan.ItemId });
            }

            foreach (var error in result.Errors)
            {
                // The form asks for a date; the request carries it as a UTC instant.
                var key = error.Key == nameof(CreateInstallmentPlanRequest.FirstDueUtc)
                    ? nameof(model.FirstDueDate)
                    : error.Key;

                ModelState.AddModelError(key, error.Value);
            }
        }

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Shows the page that takes the plan's down payment by card.
    /// </summary>
    /// <param name="itemId">The plan identifier.</param>
    public async Task<IActionResult> Pay(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var plan = await FindPlanAsync(itemId);

        if (plan is null)
        {
            return NotFound();
        }

        if (plan.Status != InstallmentPlanStatus.Draft)
        {
            return RedirectToAction(nameof(Detail), new { itemId });
        }

        var session = string.IsNullOrEmpty(plan.DownPaymentSessionId)
            ? null
            : await _sessionStore.GetAsync(plan.DownPaymentSessionId, HttpContext.RequestAborted);

        if (session is null || session.Status is CheckoutSessionStatus.Canceled or CheckoutSessionStatus.Expired or CheckoutSessionStatus.Failed)
        {
            await _notifier.WarningAsync(H["The down payment for this plan can no longer be taken. Cancel the plan and create it again."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        return View(new InstallmentPlanPayViewModel
        {
            Plan = plan,
            Flow = new CheckoutFlow(session),
            PaymentMethods = [.. GetEligiblePaymentMethods().Select(provider => new InstallmentPlanPaymentMethodViewModel
            {
                Key = provider.Key,
                DisplayName = provider.DisplayName,
            })],
        });
    }

    /// <summary>
    /// Starts the down payment with the card the administrator entered.
    /// </summary>
    /// <param name="itemId">The plan identifier.</param>
    /// <param name="request">The payment method and what its script produced.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BeginPayment(string itemId, [FromBody] BeginDownPaymentRequest request)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var plan = await FindPlanAsync(itemId);

        if (plan is null || plan.Status != InstallmentPlanStatus.Draft || string.IsNullOrEmpty(plan.DownPaymentSessionId))
        {
            return Error(S["This plan is not waiting for a down payment."], StatusCodes.Status400BadRequest);
        }

        var provider = GetEligiblePaymentMethods().FirstOrDefault(candidate =>
            string.Equals(candidate.Key, request?.ProviderKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return Error(S["Choose a payment method that can keep the card for later payments."], StatusCodes.Status400BadRequest);
        }

        var outcome = await _checkoutEngine.BeginPaymentAsync(
            plan.DownPaymentSessionId,
            new BeginPaymentOptions
            {
                ProviderKey = provider.Key,
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl,
                ProviderData = BuildProviderData(request.ProviderData, plan.CollectionMethod == InstallmentCollectionMethod.AutoCharge),
            },
            HttpContext.RequestAborted);

        if (!outcome.Succeeded)
        {
            return Error(outcome.ProviderErrorMessage ?? outcome.ErrorMessage, StatusCodes.Status400BadRequest);
        }

        return Ok(new
        {
            requiresAction = outcome.RequiresAction,
            steps = outcome.Steps.Select(step => new
            {
                obligationId = step.ObligationId,
                attemptId = step.AttemptId,
                clientSecret = step.ClientSecret,
                redirectUrl = step.RedirectUrl,
                requiresAction = step.RequiresAction,
            }),
        });
    }

    /// <summary>
    /// Reports whether the down payment settled and, once it has, starts the plan.
    /// </summary>
    /// <param name="itemId">The plan identifier.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PaymentStatus(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var plan = await FindPlanAsync(itemId);

        if (plan is null || string.IsNullOrEmpty(plan.DownPaymentSessionId))
        {
            return NotFound();
        }

        var result = await _checkoutEngine.TryCompleteAsync(plan.DownPaymentSessionId, HttpContext.RequestAborted);

        if (result.IsCompleted)
        {
            // The payment handler usually starts the plan as the payment is recorded; this makes sure of it before
            // the administrator is shown the plan.
            await _planService.ActivateAsync(plan.ItemId, HttpContext.RequestAborted);
        }

        return Ok(new
        {
            status = result.Status.ToString(),
            completed = result.IsCompleted,
            blockingStepKey = result.BlockingStepKey,
            errorMessage = result.ErrorMessage,
        });
    }

    /// <summary>
    /// Shows a plan.
    /// </summary>
    /// <param name="itemId">The plan identifier.</param>
    public async Task<IActionResult> Detail(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var plan = await FindPlanAsync(itemId);

        if (plan is null)
        {
            return NotFound();
        }

        var transactions = new Dictionary<string, Transaction>(StringComparer.Ordinal);

        foreach (var payment in plan.Payments.Where(payment => !string.IsNullOrEmpty(payment.TransactionId)))
        {
            var transaction = await _transactionManager.FindByIdAsync(payment.TransactionId);

            if (transaction is not null)
            {
                transactions[transaction.ItemId] = transaction;
            }
        }

        return View(new InstallmentPlanDetailViewModel
        {
            Plan = plan,
            Transactions = transactions,
            ShowReceipts = await IsFeatureEnabledAsync(TransactionsConstants.Features.Receipts),
            UtcNow = _clock.UtcNow,
        });
    }

    /// <summary>
    /// Charges the saved card for one payment now.
    /// </summary>
    /// <param name="itemId">The plan identifier.</param>
    /// <param name="paymentNumber">The payment to charge.</param>
    [HttpPost]
    public async Task<IActionResult> Charge(string itemId, int paymentNumber)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var result = await _planService.ChargeAsync(itemId, paymentNumber, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            var payment = result.Plan?.Payments.FirstOrDefault(candidate => candidate.Number == paymentNumber);

            if (payment?.Status == InstallmentPaymentStatus.Paid)
            {
                await _notifier.SuccessAsync(H["Payment {0} was charged and received.", paymentNumber]);
            }
            else
            {
                await _notifier.InformationAsync(H["Payment {0} was sent to the card and is still being confirmed. This page updates once it is.", paymentNumber]);
            }
        }
        else
        {
            await _notifier.ErrorAsync(H["The payment could not be charged: {0}", string.Join(" ", result.Errors.Select(error => error.Value))]);
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    /// <summary>
    /// Cancels a plan.
    /// </summary>
    /// <param name="itemId">The plan identifier.</param>
    /// <param name="reason">An optional reason.</param>
    [HttpPost]
    public async Task<IActionResult> Cancel(string itemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Forbid();
        }

        var result = await _planService.CancelAsync(itemId, reason, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The plan was canceled. Payments not yet received will not be collected."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The plan could not be canceled: {0}", string.Join(" ", result.Errors.Select(error => error.Value))]);
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    private async Task<bool> IsFeatureEnabledAsync(string featureId)
    {
        var features = await HttpContext.RequestServices.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync();

        return features.Any(feature => string.Equals(feature.Id, featureId, StringComparison.Ordinal));
    }

    private async Task<InstallmentPlan> FindPlanAsync(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : await _planStore.FindByIdAsync(itemId, HttpContext.RequestAborted);

    // Only a provider that can keep the card, and whose card form can be shown on this page, can take a down payment
    // that the schedule then charges.
    private IEnumerable<ICheckoutPaymentProvider> GetEligiblePaymentMethods()
        => _providerResolver.GetProviders().Where(provider =>
            provider.Capabilities.SupportsSavedPaymentMethods &&
            provider.Capabilities.SupportsEmbeddedElements &&
            _savedPaymentMethodProviders.Any(saved => string.Equals(saved.Key, provider.Key, StringComparison.OrdinalIgnoreCase)));

    // What the browser sent is bounded, and the keys that decide whether a saved card is spent are stripped: only
    // this server decides to keep a card, and nothing a browser sends may charge one without the customer.
    private static Dictionary<string, string> BuildProviderData(Dictionary<string, string> providerData, bool savePaymentMethod)
    {
        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in providerData ?? [])
        {
            if (string.IsNullOrEmpty(pair.Key) ||
                pair.Key.Length > MaxProviderDataKeyLength ||
                (pair.Value is not null && pair.Value.Length > MaxProviderDataValueLength) ||
                pair.Key is CheckoutPaymentDataKeys.OffSession or CheckoutPaymentDataKeys.SavedCustomerReference or CheckoutPaymentDataKeys.SavedPaymentMethodReference or CheckoutPaymentDataKeys.SavePaymentMethod)
            {
                continue;
            }

            sanitized[pair.Key] = pair.Value;

            if (sanitized.Count == MaxProviderDataEntries)
            {
                break;
            }
        }

        if (savePaymentMethod)
        {
            sanitized[CheckoutPaymentDataKeys.SavePaymentMethod] = "true";
        }

        return sanitized;
    }

    private async Task PopulateAsync(CreateInstallmentPlanViewModel model)
    {
        var currencies = await _currencyProvider.GetCurrenciesAsync();

        model.Currencies = [.. currencies
            .OrderBy(currency => currency.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .Select(currency => new SelectListItem($"{currency.CurrencyCode} — {currency.DisplayName}", currency.CurrencyCode)
            {
                Selected = string.Equals(currency.CurrencyCode, model.Currency, StringComparison.OrdinalIgnoreCase),
            })];

        model.CurrencyDecimals = currencies.ToDictionary(
            currency => currency.CurrencyCode,
            currency => CurrencyScale.GetDecimalPlaces(currency.CurrencyCode),
            StringComparer.OrdinalIgnoreCase);
    }

    private List<SelectListItem> BuildStatusItems(InstallmentPlanStatus? selected)
    {
        var items = new List<SelectListItem>
        {
            new(S["All statuses"], string.Empty, !selected.HasValue),
        };

        foreach (var status in Enum.GetValues<InstallmentPlanStatus>())
        {
            items.Add(new SelectListItem(GetStatusLabel(status), status.ToString(), selected == status));
        }

        return items;
    }

    private string GetStatusLabel(InstallmentPlanStatus status)
        => status switch
        {
            InstallmentPlanStatus.Draft => S["Waiting for down payment"],
            InstallmentPlanStatus.Active => S["Active"],
            InstallmentPlanStatus.PastDue => S["Past due"],
            InstallmentPlanStatus.Completed => S["Completed"],
            InstallmentPlanStatus.Canceled => S["Canceled"],
            _ => status.ToString(),
        };

    private static JsonResult Error(string message, int statusCode)
        => new(new { error = message, errorMessage = message })
        {
            StatusCode = statusCode,
        };

    /// <summary>
    /// The body of a down payment begin request.
    /// </summary>
    public sealed class BeginDownPaymentRequest
    {
        /// <summary>
        /// Gets or sets the chosen payment provider.
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// Gets or sets where a hosted provider returns after paying.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        /// Gets or sets where a hosted provider returns after canceling.
        /// </summary>
        public string CancelUrl { get; set; }

        /// <summary>
        /// Gets or sets what the provider's script produced, such as a tokenized card.
        /// </summary>
        public Dictionary<string, string> ProviderData { get; set; }
    }
}
