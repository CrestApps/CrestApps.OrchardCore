using System.Security.Claims;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Users.Services;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// Provides the administration report and management actions for the provider-agnostic transaction ledger.
/// </summary>
[Admin("transactions/{action}/{itemId?}", "Transactions{action}")]
public sealed class AdminController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _optionsStatus = "Options.Status";
    private const string _optionsSource = "Options.Source";

    private readonly ITransactionManager _transactionManager;
    private readonly ITransactionCommandService _commandService;
    private readonly ITransactionReminderService _reminderService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserService _userService;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IClock _clock;
    private readonly INotifier _notifier;
    private readonly TransactionSourceOptions _sourceOptions;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IDisplayManager<Transaction> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ILocalClock _localClock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="commandService">The service that runs the payment, cancel, adjust and invoice actions.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="userService">The user service used to resolve transaction owners.</param>
    /// <param name="displayNameProvider">The display name provider used to describe owners.</param>
    /// <param name="clock">The clock used to timestamp notes.</param>
    /// <param name="notifier">The notifier used to surface confirmation messages.</param>
    /// <param name="sourceOptions">The registered transaction sources used to build the source filter.</param>
    /// <param name="displayManager">The display manager that builds the detail page from the display drivers of a transaction.</param>
    /// <param name="updateModelAccessor">The accessor of the model updater the display drivers are given.</param>
    /// <param name="localClock">The clock that converts a time entered in the site's time zone to UTC.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="localizerFactory">The localizer factory that translates the registered transaction sources' names.</param>
    /// <param name="reminderService">The optional reminder service, available only when the Transaction Reminders feature is enabled.</param>
    public AdminController(
        ITransactionManager transactionManager,
        ITransactionCommandService commandService,
        IAuthorizationService authorizationService,
        IUserService userService,
        IDisplayNameProvider displayNameProvider,
        IClock clock,
        INotifier notifier,
        IOptions<TransactionSourceOptions> sourceOptions,
        IDisplayManager<Transaction> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        ILocalClock localClock,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer,
        IStringLocalizerFactory localizerFactory,
        ITransactionReminderService reminderService = null)
    {
        _transactionManager = transactionManager;
        _commandService = commandService;
        _reminderService = reminderService;
        _authorizationService = authorizationService;
        _userService = userService;
        _displayNameProvider = displayNameProvider;
        _clock = clock;
        _notifier = notifier;
        _sourceOptions = sourceOptions.Value;
        _localizerFactory = localizerFactory;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _localClock = localClock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays the transactions report.
    /// </summary>
    /// <param name="options">The filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("transactions", "TransactionsIndex")]
    public async Task<IActionResult> Index(
        TransactionsAdminIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);

        var query = BuildQuery(options);

        var result = await _transactionManager.PageAsync(pager.Page, pager.PageSize, query);

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status != TransactionStatusFilter.All)
        {
            routeData.Values.TryAdd(_optionsStatus, options.Status);
        }

        if (!string.IsNullOrEmpty(options.Source))
        {
            routeData.Values.TryAdd(_optionsSource, options.Source);
        }

        options.Statuses = BuildStatusFilterItems(options.Status);
        options.Sources = BuildSourceFilterItems(options.Source);

        // The status and source filters render at the end of the list's toolbar.
        var filters = await shapeFactory.CreateAdminListFiltersAsync("Transactions", "transactions-filter", new AdminListFilter("Options.Status", options.Statuses, S["Filter by status"], S["Status"]), new AdminListFilter("Options.Source", options.Sources, S["Filter by source"], S["Source"]));

        var model = new TransactionsAdminIndexViewModel
        {
            Options = options,
            List = new AdminListContext<Transaction>
            {
                Source = result.Entries,
                ToolbarActions = filters,
                Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            },
        };

        return View(model);
    }

    /// <summary>
    /// Preserves the report filter when the toolbar is submitted.
    /// </summary>
    /// <param name="options">The filter options.</param>
    /// <param name="pagerParameters">The page size to keep while filtering.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("transactions", "TransactionsIndex")]
    public async Task<IActionResult> IndexFilterPost(TransactionsAdminIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeValues.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status != TransactionStatusFilter.All)
        {
            routeValues.TryAdd(_optionsStatus, options.Status);
        }

        if (!string.IsNullOrEmpty(options.Source))
        {
            routeValues.TryAdd(_optionsSource, options.Source);
        }

        if (pagerParameters.PageSize.HasValue)
        {
            routeValues.TryAdd("pageSize", pagerParameters.PageSize.Value);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Displays a single transaction and its audit timeline.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    public async Task<IActionResult> Detail(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(itemId))
        {
            return NotFound();
        }

        var transaction = await _transactionManager.FindByIdAsync(itemId);

        if (transaction is null)
        {
            return NotFound();
        }

        // The page is built by the display drivers of Transaction, so another module can add a card to it.

        return View(new TransactionDetailPageViewModel
        {
            Transaction = transaction,
            Shape = await _displayManager.BuildDisplayAsync(transaction, _updateModelAccessor.ModelUpdater, TransactionsConstants.DisplayTypes.DetailAdmin),
        });
    }

    /// <summary>
    /// Sends a manual payment reminder to the transaction owner.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    [HttpPost]
    public async Task<IActionResult> SendReminder(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        if (_reminderService is null)
        {
            await _notifier.WarningAsync(H["Reminders are unavailable. Enable the Transaction Reminders feature to send reminders."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        var transaction = await _transactionManager.FindByIdAsync(itemId);

        if (transaction is null)
        {
            return NotFound();
        }

        if (!TransactionStateMachine.CanSendReminder(transaction))
        {
            await _notifier.WarningAsync(H["This transaction has no outstanding balance to chase, so no reminder was sent."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        if (await _reminderService.SendReminderAsync(transaction))
        {
            if (!await TrySaveAsync(transaction))
            {
                return RedirectToAction(nameof(Detail), new { itemId });
            }

            await _notifier.SuccessAsync(H["A payment reminder was sent to the transaction owner."]);
        }
        else
        {
            await _notifier.WarningAsync(H["The reminder could not be sent. The owner may not have a notification channel configured."]);
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    /// <summary>
    /// Records an offline payment against a transaction.
    /// </summary>
    /// <param name="model">The recorded payment.</param>
    [HttpPost]
    public async Task<IActionResult> RecordPayment(RecordPaymentViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        // The received time is entered in the site's time zone and stored in UTC.
        var receivedUtc = model.ReceivedAt.HasValue
            ? await _localClock.ToUtcLenientAsync(model.ReceivedAt.Value)
            : (DateTime?)null;

        var result = await _commandService.RecordPaymentAsync(model.TransactionId, new TransactionPaymentRequest
        {
            Amount = model.Amount,
            PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? null : model.PaymentMethod.Trim(),
            ReceivedUtc = receivedUtc,
            Reference = model.Reference,
            Note = model.Note,
            CreditExcess = model.CreditExcess,
        });

        if (result.Succeeded && result.UnappliedAmount > 0m)
        {
            // Nothing above the balance is recorded unless it was kept as credit, so say what is left to give back.
            await _notifier.WarningAsync(H["{0} {1} was more than was owed and was not recorded. Give it back, or record the payment again keeping the rest as customer credit.", CurrencyScale.Format(result.UnappliedAmount, result.Transaction.Currency), result.Transaction.Currency]);
        }

        return await CompleteAsync(result, model.TransactionId, result.CreditedAmount > 0m
            ? H["The payment was recorded, and {0} {1} above what was owed was added to the customer's credit.", CurrencyScale.Format(result.CreditedAmount, result.Transaction.Currency), result.Transaction.Currency]
            : H["The payment was recorded."]);
    }

    /// <summary>
    /// Pays toward a transaction with its payer's customer credit.
    /// </summary>
    /// <param name="model">The credit to apply.</param>
    [HttpPost]
    public async Task<IActionResult> ApplyCredit(ApplyCreditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var result = await _commandService.ApplyCreditAsync(model.TransactionId, model.Amount, model.Note);

        return await CompleteAsync(result, model.TransactionId, H["The customer's credit was applied."]);
    }

    /// <summary>
    /// Takes back a payment recorded by hand or an adjustment with a reversing entry, so it is owed again. Neither entry is
    /// removed from the timeline.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="eventId">The id of the timeline entry to take back.</param>
    /// <param name="reason">Why the entry is taken back; required.</param>
    [HttpPost]
    public async Task<IActionResult> Reverse(string itemId, string eventId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var result = await _commandService.ReverseAsync(itemId, eventId, reason);

        return await CompleteAsync(result, itemId, H["The entry was taken back. What it paid or took off is owed again."]);
    }

    /// <summary>
    /// Marks a transaction as fully paid and settled.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="note">An optional note recorded with the payment.</param>
    [HttpPost]
    public async Task<IActionResult> MarkPaid(string itemId, string note)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var result = await _commandService.MarkPaidAsync(itemId, note);

        return await CompleteAsync(result, itemId, H["The transaction was marked as paid."]);
    }

    /// <summary>
    /// Pauses or resumes the scheduled reminders of a transaction.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="paused">Whether its reminders are paused.</param>
    [HttpPost]
    public async Task<IActionResult> PauseReminders(string itemId, bool paused)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var result = await _commandService.SetRemindersPausedAsync(itemId, paused);

        return await CompleteAsync(result, itemId, paused ? H["The scheduled reminders were paused."] : H["The scheduled reminders were resumed."]);
    }

    /// <summary>
    /// Cancels the scheduled charge of the owner's saved card, so the owner pays the balance themselves.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="charges">The saved card charges.</param>
    [HttpPost]
    public async Task<IActionResult> CancelAutoCharge(string itemId, [FromServices] ISavedPaymentMethodChargeService charges)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var result = await charges.CancelAsync(itemId);

        if (result.Transaction is null && result.Errors.Count > 0)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The scheduled charge was canceled. The owner pays the balance themselves."]);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.WarningAsync(new LocalizedHtmlString(error, HtmlEncoder.Default.Encode(error)));
            }
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    /// <summary>
    /// Cancels an outstanding transaction so it is no longer collectable.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="note">An optional reason recorded on the timeline.</param>
    [HttpPost]
    public async Task<IActionResult> Cancel(string itemId, string note)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var result = await _commandService.CancelAsync(itemId, note);

        return await CompleteAsync(result, itemId, H["The transaction was canceled."]);
    }

    /// <summary>
    /// Lowers what is owed on a transaction without money being received: a discount, a waiver, a credit or a
    /// write-off.
    /// </summary>
    /// <param name="model">The adjustment.</param>
    [HttpPost]
    public async Task<IActionResult> Adjust(AdjustTransactionViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var result = await _commandService.AdjustAsync(model.TransactionId, model.Amount, model.Kind, model.Reason);

        return await CompleteAsync(result, model.TransactionId, H["The adjustment was recorded."]);
    }

    /// <summary>
    /// Sends the owner the transaction's invoice now, with the link that pays it.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    [HttpPost]
    public async Task<IActionResult> SendInvoice(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var result = await _commandService.SendInvoiceAsync(itemId);

        return await CompleteAsync(result, itemId, H["The invoice was sent to the transaction owner."]);
    }

    /// <summary>
    /// Adds a free-form note to the transaction timeline.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="note">The note to record.</param>
    [HttpPost]
    public async Task<IActionResult> AddNote(string itemId, string note)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        var transaction = await _transactionManager.FindByIdAsync(itemId);

        if (transaction is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return RedirectToAction(nameof(Detail), new { itemId });
        }

        TransactionAdminActions.AddNote(transaction, note, _clock.UtcNow, User.FindFirstValue(ClaimTypes.NameIdentifier), await GetCurrentUserNameAsync());

        if (await TrySaveAsync(transaction))
        {
            await _notifier.SuccessAsync(H["The note was added."]);
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    // Shows what came of a command: not found, each reason it was refused, or the success message.
    private async Task<IActionResult> CompleteAsync(TransactionCommandResult result, string itemId, LocalizedHtmlString success)
    {
        if (result.Transaction is null)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.WarningAsync(new LocalizedHtmlString(error, HtmlEncoder.Default.Encode(error)));
            }
        }

        // What followed the saved change but did not finish with it, such as a customer credit change left for later.
        foreach (var warning in result.Warnings)
        {
            await _notifier.WarningAsync(new LocalizedHtmlString(warning, HtmlEncoder.Default.Encode(warning)));
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    // Persists a management change, turning the optimistic-concurrency failure into a message the operator can
    // act on. The store checks document versions so two managers working the same transaction cannot silently
    // overwrite each other; without this the loser of that race saw an unhandled server error instead of being
    // told to reload and try again.
    private async Task<bool> TrySaveAsync(Transaction transaction)
    {
        try
        {
            await _transactionManager.UpdateAsync(transaction);

            return true;
        }
        catch (ConcurrencyException)
        {
            await _notifier.WarningAsync(H["This transaction was changed by someone else while you were working on it. Reload the page and try again."]);

            return false;
        }
    }

    private static TransactionQuery BuildQuery(TransactionsAdminIndexOptions options)
    {
        var query = new TransactionQuery
        {
            Search = options.Search,
            Source = options.Source,
        };

        options.Status.ApplyTo(query);

        return query;
    }

    private List<SelectListItem> BuildStatusFilterItems(TransactionStatusFilter selected)
        => TransactionStatusFilterExtensions.BuildFilterItems(selected, S);

    private List<SelectListItem> BuildSourceFilterItems(string selected)
    {
        var items = new List<SelectListItem>
        {
            new(S["Any source"], string.Empty, string.IsNullOrEmpty(selected)),
        };

        foreach (var source in _sourceOptions.Sources.Values)
        {
            var text = source.DisplayName is null ? source.Name : _localizerFactory.Localize(source.DisplayName).Value;

            items.Add(new SelectListItem(text, source.Name, string.Equals(source.Name, selected, StringComparison.OrdinalIgnoreCase)));
        }

        return items;
    }

    private async Task<string> ResolveOwnerNameAsync(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId))
        {
            return null;
        }

        var user = await _userService.GetUserByUniqueIdAsync(ownerId);

        if (user is null)
        {
            return null;
        }

        return await _displayNameProvider.GetAsync(user);
    }

    private async Task<string> GetCurrentUserNameAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return await ResolveOwnerNameAsync(userId);
    }
}
