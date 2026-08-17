using System.Security.Claims;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Users.Services;

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
    private readonly ITransactionReminderService _reminderService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserService _userService;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="reminderService">The reminder service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="userService">The user service used to resolve transaction owners.</param>
    /// <param name="displayNameProvider">The display name provider used to describe owners.</param>
    /// <param name="clock">The clock used to timestamp management events.</param>
    /// <param name="notifier">The notifier used to surface confirmation messages.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminController(
        ITransactionManager transactionManager,
        ITransactionReminderService reminderService,
        IAuthorizationService authorizationService,
        IUserService userService,
        IDisplayNameProvider displayNameProvider,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _reminderService = reminderService;
        _authorizationService = authorizationService;
        _userService = userService;
        _displayNameProvider = displayNameProvider;
        _clock = clock;
        _notifier = notifier;
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

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var query = BuildQuery(options);

        var result = await _transactionManager.PageAsync(pager.Page, pager.PageSize, query);

        var outstandingResult = await _transactionManager.PageAsync(1, int.MaxValue, new TransactionQuery
        {
            OutstandingOnly = true,
            Source = options.Source,
            Search = options.Search,
        });

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

        var model = new TransactionsAdminIndexViewModel
        {
            Options = options,
            TotalOutstanding = outstandingResult.Entries.Sum(x => x.OutstandingAmount),
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var transaction in result.Entries)
        {
            model.Transactions.Add(new TransactionListItemViewModel
            {
                Transaction = transaction,
                OwnerName = await ResolveOwnerNameAsync(transaction.OwnerId),
            });
        }

        return View(model);
    }

    /// <summary>
    /// Preserves the report filter when the toolbar is submitted.
    /// </summary>
    /// <param name="options">The filter options.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("transactions", "TransactionsIndex")]
    public async Task<IActionResult> IndexFilterPost(TransactionsAdminIndexOptions options)
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

        var model = new TransactionDetailViewModel
        {
            Transaction = transaction,
            OwnerName = await ResolveOwnerNameAsync(transaction.OwnerId),
            CanManage = true,
        };

        return View(model);
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

        var transaction = await _transactionManager.FindByIdAsync(itemId);

        if (transaction is null)
        {
            return NotFound();
        }

        if (transaction.OutstandingAmount <= 0m)
        {
            await _notifier.WarningAsync(H["This transaction has no outstanding balance, so no reminder was sent."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        if (await _reminderService.SendReminderAsync(transaction))
        {
            await _transactionManager.UpdateAsync(transaction);
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

        var transaction = await _transactionManager.FindByIdAsync(model.TransactionId);

        if (transaction is null)
        {
            return NotFound();
        }

        if (model.Amount <= 0m)
        {
            await _notifier.WarningAsync(H["Enter a payment amount greater than zero."]);

            return RedirectToAction(nameof(Detail), new { itemId = model.TransactionId });
        }

        var now = _clock.UtcNow;
        var applied = Math.Min(model.Amount, transaction.OutstandingAmount);

        transaction.AmountPaid += applied;
        transaction.UpdatedUtc = now;
        transaction.SettlementMethod = TransactionsConstants.SettlementMethods.Offline;

        var noteSuffix = string.IsNullOrWhiteSpace(model.Note)
            ? string.Empty
            : S[" Note: {0}", model.Note].Value;

        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.PaymentRecorded,
            Message = S["An offline payment of {0} {1} was recorded.{2}", transaction.Currency, applied.ToString("0.00"), noteSuffix].Value,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = await GetCurrentUserNameAsync(),
        });

        if (transaction.OutstandingAmount <= 0m)
        {
            transaction.Status = TransactionStatus.Paid;
            transaction.SettledUtc = now;
            transaction.Events.Add(new TransactionEvent
            {
                CreatedUtc = now,
                Type = TransactionEventType.StatusChanged,
                Message = S["The transaction was fully paid and settled."].Value,
            });
        }
        else if (transaction.AmountPaid > 0m)
        {
            transaction.Status = TransactionStatus.PartiallyPaid;
        }

        await _transactionManager.UpdateAsync(transaction);
        await _notifier.SuccessAsync(H["The payment was recorded."]);

        return RedirectToAction(nameof(Detail), new { itemId = model.TransactionId });
    }

    /// <summary>
    /// Marks a transaction as fully paid and settled.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    [HttpPost]
    public async Task<IActionResult> MarkPaid(string itemId)
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

        var now = _clock.UtcNow;

        transaction.AmountPaid = transaction.TotalAmount;
        transaction.Status = TransactionStatus.Paid;
        transaction.SettledUtc = now;
        transaction.UpdatedUtc = now;
        transaction.SettlementMethod = TransactionsConstants.SettlementMethods.Offline;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.StatusChanged,
            Message = S["The transaction was marked as paid."].Value,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = await GetCurrentUserNameAsync(),
        });

        await _transactionManager.UpdateAsync(transaction);
        await _notifier.SuccessAsync(H["The transaction was marked as paid."]);

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

        var transaction = await _transactionManager.FindByIdAsync(itemId);

        if (transaction is null)
        {
            return NotFound();
        }

        var now = _clock.UtcNow;

        transaction.Status = TransactionStatus.Canceled;
        transaction.UpdatedUtc = now;

        var reason = string.IsNullOrWhiteSpace(note)
            ? S["The transaction was canceled."].Value
            : S["The transaction was canceled. Reason: {0}", note].Value;

        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Canceled,
            Message = reason,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = await GetCurrentUserNameAsync(),
        });

        await _transactionManager.UpdateAsync(transaction);
        await _notifier.SuccessAsync(H["The transaction was canceled."]);

        return RedirectToAction(nameof(Detail), new { itemId });
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

        var now = _clock.UtcNow;

        transaction.UpdatedUtc = now;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Note,
            Message = note,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = await GetCurrentUserNameAsync(),
        });

        await _transactionManager.UpdateAsync(transaction);
        await _notifier.SuccessAsync(H["The note was added."]);

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    private static TransactionQuery BuildQuery(TransactionsAdminIndexOptions options)
    {
        var query = new TransactionQuery
        {
            Search = options.Search,
            Source = options.Source,
        };

        switch (options.Status)
        {
            case TransactionStatusFilter.All:
                break;
            case TransactionStatusFilter.Outstanding:
                query.OutstandingOnly = true;
                break;
            case TransactionStatusFilter.Pending:
                query.Status = TransactionStatus.Pending;
                break;
            case TransactionStatusFilter.PartiallyPaid:
                query.Status = TransactionStatus.PartiallyPaid;
                break;
            case TransactionStatusFilter.Paid:
                query.Status = TransactionStatus.Paid;
                break;
            case TransactionStatusFilter.Canceled:
                query.Status = TransactionStatus.Canceled;
                break;
            case TransactionStatusFilter.Failed:
                query.Status = TransactionStatus.Failed;
                break;
            case TransactionStatusFilter.Abandoned:
                query.Status = TransactionStatus.Abandoned;
                break;
            case TransactionStatusFilter.Refunded:
                query.Status = TransactionStatus.Refunded;
                break;
        }

        return query;
    }

    private List<SelectListItem> BuildStatusFilterItems(TransactionStatusFilter selected)
    {
        return
        [
            new SelectListItem(S["All"], nameof(TransactionStatusFilter.All), selected == TransactionStatusFilter.All),
            new SelectListItem(S["Outstanding"], nameof(TransactionStatusFilter.Outstanding), selected == TransactionStatusFilter.Outstanding),
            new SelectListItem(S["Pending"], nameof(TransactionStatusFilter.Pending), selected == TransactionStatusFilter.Pending),
            new SelectListItem(S["Partially paid"], nameof(TransactionStatusFilter.PartiallyPaid), selected == TransactionStatusFilter.PartiallyPaid),
            new SelectListItem(S["Paid"], nameof(TransactionStatusFilter.Paid), selected == TransactionStatusFilter.Paid),
            new SelectListItem(S["Canceled"], nameof(TransactionStatusFilter.Canceled), selected == TransactionStatusFilter.Canceled),
            new SelectListItem(S["Failed"], nameof(TransactionStatusFilter.Failed), selected == TransactionStatusFilter.Failed),
            new SelectListItem(S["Abandoned"], nameof(TransactionStatusFilter.Abandoned), selected == TransactionStatusFilter.Abandoned),
            new SelectListItem(S["Refunded"], nameof(TransactionStatusFilter.Refunded), selected == TransactionStatusFilter.Refunded),
        ];
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
