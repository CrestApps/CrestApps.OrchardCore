using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Locking;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The default <see cref="IInstallmentPlanService"/>.
/// </summary>
/// <remarks>
/// Every change to a plan happens under a per-plan distributed lock and is committed before the lock is released,
/// because the schedule sweep, the payment handler and an administrator all act on the same plan. A payment is
/// only ever charged through <see cref="ICheckoutEngine"/>, so the ledger, verification against the gateway and
/// refunds are the same as for any other payment.
/// </remarks>
public sealed class DefaultInstallmentPlanService : IInstallmentPlanService
{
    private const int MaxInstallments = 120;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan _transientRetryDelay = TimeSpan.FromHours(1);

    private readonly IInstallmentPlanStore _planStore;
    private readonly ITransactionManager _transactionManager;
    private readonly ICheckoutEngine _checkoutEngine;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly IEnumerable<ICheckoutSavedPaymentMethodProvider> _savedPaymentMethodProviders;
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly ITransactionSettlementService _settlementService;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly UserManager<IUser> _userManager;
    private readonly IUserService _userService;
    private readonly IDistributedLock _distributedLock;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICustomerOwnerNotifier _ownerNotifier;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultInstallmentPlanService"/> class.
    /// </summary>
    public DefaultInstallmentPlanService(
        IInstallmentPlanStore planStore,
        ITransactionManager transactionManager,
        ICheckoutEngine checkoutEngine,
        ICheckoutSessionStore sessionStore,
        ICheckoutPaymentProviderResolver providerResolver,
        IEnumerable<ICheckoutSavedPaymentMethodProvider> savedPaymentMethodProviders,
        IPaymentAttemptStore attemptStore,
        ITransactionSettlementService settlementService,
        CheckoutInvoiceBuilder invoiceBuilder,
        UserManager<IUser> userManager,
        IUserService userService,
        IDistributedLock distributedLock,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        ICustomerOwnerNotifier ownerNotifier,
        ISession session,
        IClock clock,
        ILogger<DefaultInstallmentPlanService> logger,
        IStringLocalizer<DefaultInstallmentPlanService> stringLocalizer)
    {
        _planStore = planStore;
        _transactionManager = transactionManager;
        _checkoutEngine = checkoutEngine;
        _sessionStore = sessionStore;
        _providerResolver = providerResolver;
        _savedPaymentMethodProviders = savedPaymentMethodProviders;
        _attemptStore = attemptStore;
        _settlementService = settlementService;
        _invoiceBuilder = invoiceBuilder;
        _userManager = userManager;
        _userService = userService;
        _distributedLock = distributedLock;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _ownerNotifier = ownerNotifier;
        _session = session;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<InstallmentPlanResult> CreateAsync(CreateInstallmentPlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return CreatePlanAsync(request, sourceTransaction: null, checkoutSession: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<InstallmentPlanResult> CreateFromTransactionAsync(CreateInstallmentPlanFromTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new InstallmentPlanResult();
        var option = request.Option;

        if (option is null)
        {
            return result.Fail(nameof(request.Option), S["Choose how to pay."]);
        }

        var transaction = string.IsNullOrEmpty(request.TransactionId)
            ? null
            : await _transactionManager.FindByIdAsync(request.TransactionId, cancellationToken);

        if (!InstallmentTransactionEligibility.CanSplit(transaction))
        {
            return result.Fail(nameof(request.TransactionId), S["This balance can't be paid in installments."]);
        }

        var checkoutSession = string.IsNullOrEmpty(request.CheckoutSessionId)
            ? null
            : await _sessionStore.GetAsync(request.CheckoutSessionId, cancellationToken);

        // The checkout must still be the one paying this transaction, and nothing may have been charged through it:
        // what it charges is about to change, and money already handed to the gateway was for the whole balance.
        if (checkoutSession is null ||
            checkoutSession.Status != CheckoutSessionStatus.Pending ||
            !string.Equals(checkoutSession.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(checkoutSession.ReferenceId, transaction.ItemId, StringComparison.Ordinal) ||
            (await _attemptStore.GetBySessionAsync(checkoutSession.SessionId, cancellationToken)).Any())
        {
            return result.Fail(nameof(request.CheckoutSessionId), S["This checkout can no longer be changed. Start paying the balance again."]);
        }

        var existing = await _planStore.GetBySourceTransactionAsync(transaction.ItemId, cancellationToken);

        if (existing.Any(plan => plan.Status is InstallmentPlanStatus.Active or InstallmentPlanStatus.PastDue or InstallmentPlanStatus.Completed))
        {
            return result.Fail(nameof(request.TransactionId), S["This balance is already being paid in installments."]);
        }

        // A plan still waiting for its first payment belongs to a checkout the customer left. Only one plan may replace
        // the balance, so that one is closed before this one is made.
        foreach (var abandoned in existing.Where(plan => plan.Status == InstallmentPlanStatus.Draft))
        {
            await CancelAsync(abandoned.ItemId, S["The customer chose how to pay again in a new checkout."].Value, cancellationToken);
        }

        var now = _clock.UtcNow;
        var hasParentReference = !string.IsNullOrEmpty(transaction.ParentReferenceType) && !string.IsNullOrEmpty(transaction.ParentReferenceId);
        var hasOwnReference = !string.IsNullOrEmpty(transaction.ReferenceType) && !string.IsNullOrEmpty(transaction.ReferenceId);

        var planRequest = new CreateInstallmentPlanRequest
        {
            Title = string.IsNullOrWhiteSpace(transaction.Title) ? S["Outstanding payment"].Value : transaction.Title,
            CustomerUserId = transaction.OwnerId,
            Currency = transaction.Currency,
            TotalAmount = transaction.OutstandingAmount,
            DownPaymentAmount = option.DownPaymentAmount,
            InstallmentCount = option.InstallmentCount,
            Frequency = option.Frequency,

            // With a down payment, the first scheduled payment falls due one period after it; without one, it is the
            // payment taken now.
            FirstDueUtc = option.DownPaymentAmount > 0m && Enum.IsDefined(option.Frequency)
                ? InstallmentScheduleCalculator.GetDueDate(now.Date, option.Frequency, 2)
                : default,
            CollectionMethod = InstallmentCollectionMethod.AutoCharge,

            // The plan pays for what the transaction paid for. A transaction that is itself part of a larger record
            // names that record as its parent, and the plan follows it there.
            ReferenceType = hasParentReference
                ? transaction.ParentReferenceType
                : hasOwnReference
                    ? transaction.ReferenceType
                    : null,
            ReferenceId = hasParentReference
                ? transaction.ParentReferenceId
                : hasOwnReference
                    ? transaction.ReferenceId
                    : null,
            Properties = MergeProperties(transaction.Properties, option.Properties),
        };

        return await CreatePlanAsync(planRequest, transaction, checkoutSession, cancellationToken);
    }

    // Creates a plan waiting for its first payment. A plan made from a transaction takes that payment through the
    // customer's own checkout; any other plan gets a checkout of its own.
    private async Task<InstallmentPlanResult> CreatePlanAsync(
        CreateInstallmentPlanRequest request,
        Transaction sourceTransaction,
        CheckoutSession checkoutSession,
        CancellationToken cancellationToken)
    {
        var result = new InstallmentPlanResult();
        var now = _clock.UtcNow;
        var currency = request.Currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            result.Fail(nameof(request.Title), S["Enter what the plan is for."]);
        }

        if (string.IsNullOrEmpty(currency) || currency.Length != 3)
        {
            result.Fail(nameof(request.Currency), S["Choose a currency."]);
        }

        if (request.TotalAmount <= 0m)
        {
            result.Fail(nameof(request.TotalAmount), S["The total must be greater than zero."]);
        }

        if (request.DownPaymentAmount < 0m)
        {
            result.Fail(nameof(request.DownPaymentAmount), S["The down payment can't be negative. Enter zero for no down payment."]);
        }
        else if (request.DownPaymentAmount >= request.TotalAmount)
        {
            result.Fail(nameof(request.DownPaymentAmount), S["The down payment must be less than the total; the rest is what the scheduled payments pay."]);
        }

        if (request.InstallmentCount < 1 || request.InstallmentCount > MaxInstallments)
        {
            result.Fail(nameof(request.InstallmentCount), S["Enter between 1 and {0} payments after the down payment.", MaxInstallments]);
        }

        if (!Enum.IsDefined(request.Frequency))
        {
            result.Fail(nameof(request.Frequency), S["Choose how often the payments fall due."]);
        }

        if (!Enum.IsDefined(request.CollectionMethod))
        {
            result.Fail(nameof(request.CollectionMethod), S["Choose how the payments are collected."]);
        }

        var hasDownPayment = request.DownPaymentAmount > 0m;
        var firstDueUtc = DateTime.SpecifyKind(request.FirstDueUtc, DateTimeKind.Utc);

        if (hasDownPayment)
        {
            if (firstDueUtc.Date <= now.Date)
            {
                result.Fail(nameof(request.FirstDueUtc), S["The first scheduled payment must fall due after today; the down payment is what is collected today."]);
            }
        }
        else
        {
            // Without a down payment the first scheduled payment is what is taken now, while the card is entered, so
            // it is due now and the schedule runs from today.
            if (request.FirstDueUtc != default && firstDueUtc.Date > now.Date)
            {
                result.Fail(nameof(request.FirstDueUtc), S["Without a down payment, the first payment is taken today, so it must fall due today."]);
            }

            firstDueUtc = now;
        }

        if (string.IsNullOrEmpty(request.ReferenceType) != string.IsNullOrEmpty(request.ReferenceId))
        {
            result.Fail(nameof(request.ReferenceType), S["Set both the reference type and the reference identifier, or neither."]);
        }

        if (result.Succeeded)
        {
            var amounts = InstallmentScheduleCalculator.SplitAmounts(request.TotalAmount, request.DownPaymentAmount, request.InstallmentCount, currency);

            if (amounts.Count == 0 || amounts.Any(amount => amount <= 0m))
            {
                result.Fail(nameof(request.InstallmentCount), S["The balance is too small to split into that many payments."]);
            }
        }

        if (!GetSavedPaymentMethodProviders().Any())
        {
            // The first payment is taken by card and the card kept for the schedule; with no provider that can do
            // that, there is no way to collect anything.
            result.Fail(string.Empty, S["No payment provider that can keep a card for later payments is enabled. Enable and configure one, such as Stripe, first."]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var (customer, customerError) = await ResolveCustomerAsync(request);

        if (customer is null)
        {
            return result.Fail(customerError.Key, customerError.Value);
        }

        var customerId = await _userManager.GetUserIdAsync(customer);
        var customerEmail = (customer as User)?.Email;
        var customerName = !string.IsNullOrWhiteSpace(request.NewCustomerName) && string.IsNullOrEmpty(request.CustomerUserId)
            ? request.NewCustomerName.Trim()
            : customer.UserName;

        var title = request.Title.Trim();
        var total = CurrencyScale.Round(request.TotalAmount, currency);
        var downPayment = CurrencyScale.Round(request.DownPaymentAmount, currency);

        var plan = new InstallmentPlan
        {
            ItemId = IdGenerator.GenerateId(),
            Title = title,
            ReferenceType = string.IsNullOrEmpty(request.ReferenceType) ? null : request.ReferenceType,
            ReferenceId = string.IsNullOrEmpty(request.ReferenceId) ? null : request.ReferenceId,
            SourceTransactionId = sourceTransaction?.ItemId,
            OwnerId = customerId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            Currency = currency,
            TotalAmount = total,
            DownPaymentAmount = downPayment,
            InstallmentCount = request.InstallmentCount,
            Frequency = request.Frequency,
            FirstDueUtc = firstDueUtc,
            CollectionMethod = request.CollectionMethod,
            Status = InstallmentPlanStatus.Draft,
            Notes = request.Notes,
            CreatedById = CurrentUserId(),
            CreatedByName = CurrentUserName(),
            CreatedUtc = now,
            UpdatedUtc = now,
            Properties = CopyProperties(request.Properties),
        };

        if (hasDownPayment)
        {
            plan.Payments.Add(new InstallmentPlanPayment
            {
                Number = 0,
                DueUtc = now,
                Amount = downPayment,
                Status = InstallmentPaymentStatus.Due,
            });
        }

        foreach (var payment in InstallmentScheduleCalculator.BuildSchedule(total, downPayment, request.InstallmentCount, request.Frequency, firstDueUtc, currency))
        {
            plan.Payments.Add(payment);
        }

        var firstPayment = plan.FirstPayment;

        firstPayment.Status = InstallmentPaymentStatus.Due;

        // The first payment is an ordinary debt the checkout settles, so it is on the ledger, receipted, and refundable
        // like any other payment. One taken in the customer's own checkout is pending rather than outstanding: it is
        // being paid right now, and is not chased with reminders if the customer walks away.
        var firstTransaction = await CreateTransactionAsync(
            plan,
            firstPayment,
            hasDownPayment
                ? S["{0} — down payment", title].Value
                : S["{0} — payment {1} of {2}", title, firstPayment.Number, plan.InstallmentCount].Value,
            checkoutSession is null ? TransactionStatus.Outstanding : TransactionStatus.Pending,
            autoCollection: null,
            now);

        firstPayment.TransactionId = firstTransaction.ItemId;

        if (checkoutSession is null)
        {
            var session = await _checkoutEngine.StartAsync(
                new StartCheckoutRequest
                {
                    ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                    ReferenceId = firstTransaction.ItemId,
                    OwnerId = customerId,
                    Contact = new CheckoutContactInfo
                    {
                        DisplayName = customerName,
                        Email = customerEmail,
                    },
                },
                cancellationToken);

            plan.DownPaymentSessionId = session.SessionId;
        }
        else
        {
            await MoveCheckoutOntoFirstPaymentAsync(checkoutSession, firstTransaction, customerName, customerEmail, now, cancellationToken);

            plan.DownPaymentSessionId = checkoutSession.SessionId;
        }

        var created = hasDownPayment
            ? S["The plan was created: {0} today, then {1} payments.", Format(downPayment, currency), request.InstallmentCount].Value
            : S["The plan was created: {0} payments, the first of {1} taken today.", request.InstallmentCount, Format(firstPayment.Amount, currency)].Value;

        if (sourceTransaction is not null)
        {
            created = S["{0} The customer chose it at checkout to pay '{1}' in installments.", created, sourceTransaction.Title].Value;
        }

        AddEvent(plan, InstallmentPlanEventType.Created, created, actor: plan.CreatedByName);

        await _planStore.CreateAsync(plan, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return InstallmentPlanResult.Success(plan);
    }

    // Points the customer's checkout at the plan's first payment: it now collects that payment rather than the whole
    // balance, and keeps the card for the rest. The invoice is rebuilt from the changed step, so the amount the
    // customer is shown is the amount that is charged.
    private async Task MoveCheckoutOntoFirstPaymentAsync(
        CheckoutSession checkoutSession,
        Transaction firstTransaction,
        string customerName,
        string customerEmail,
        DateTime now,
        CancellationToken cancellationToken)
    {
        checkoutSession.ReferenceType = TransactionsConstants.ReferenceTypes.Transaction;
        checkoutSession.ReferenceId = firstTransaction.ItemId;
        checkoutSession.SettlesExistingBalance = true;
        checkoutSession.SavePaymentMethod = true;
        checkoutSession.ModifiedUtc = now;

        // The card is kept on a customer at the gateway, which is named after the plan's customer rather than whoever
        // opened the pay link.
        if (!checkoutSession.TryGet<CheckoutContactInfo>(out _) && !string.IsNullOrEmpty(customerEmail))
        {
            checkoutSession.Put(new CheckoutContactInfo
            {
                DisplayName = customerName,
                Email = customerEmail,
            });
        }

        var step = checkoutSession.Steps.FirstOrDefault(candidate => string.Equals(candidate.Key, TransactionsConstants.SettlementStepKey, StringComparison.Ordinal));

        if (step is null)
        {
            step = new CheckoutFlowStep
            {
                Key = TransactionsConstants.SettlementStepKey,
                Title = S["Outstanding payment"],
                Order = 0,
                CollectData = false,
                Conceal = true,
            };

            checkoutSession.Steps.Add(step);
        }

        step.Description = firstTransaction.Title;
        step.BillingItems =
        [
            new BillingItem
            {
                ItemId = firstTransaction.ItemId,
                Description = firstTransaction.Title,
                Amount = firstTransaction.TotalAmount,
                Plan = null,

                // The plan splits a balance whose tax was decided when it was raised, so it is not taxed again.
                ExcludeFromTax = true,
            },
        ];

        await _invoiceBuilder.BuildAsync(new CheckoutFlow(checkoutSession));
        await _sessionStore.SaveAsync(checkoutSession, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<InstallmentPlanResult> ActivateAsync(string planId, CancellationToken cancellationToken = default)
        => MutateAsync(planId, waitForLock: true, async plan =>
        {
            var result = InstallmentPlanResult.Success(plan);

            if (plan.Status != InstallmentPlanStatus.Draft)
            {
                return result;
            }

            if (!await TryActivateAsync(plan, cancellationToken))
            {
                return result.Fail(string.Empty, S["The down payment has not been received yet."]);
            }

            return result;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<InstallmentPlanResult> ChargeAsync(string planId, int paymentNumber, CancellationToken cancellationToken = default)
        => MutateAsync(planId, waitForLock: true, async plan =>
        {
            var result = InstallmentPlanResult.Success(plan);

            if (plan.Status is not (InstallmentPlanStatus.Active or InstallmentPlanStatus.PastDue))
            {
                return result.Fail(string.Empty, S["Only an active plan's payments can be charged."]);
            }

            var payment = plan.Payments.FirstOrDefault(candidate => candidate.Number == paymentNumber && candidate.Number > 0);

            if (payment is null)
            {
                return result.Fail(string.Empty, S["The payment could not be found."]);
            }

            await SyncPaymentsAsync(plan, cancellationToken);

            if (payment.Status is InstallmentPaymentStatus.Paid or InstallmentPaymentStatus.Canceled)
            {
                return result.Fail(string.Empty, S["This payment does not need collecting."]);
            }

            if (plan.PaymentMethod is null)
            {
                return result.Fail(string.Empty, S["There is no saved card on this plan to charge."]);
            }

            var outcome = await ChargePaymentAsync(plan, payment, manual: true, cancellationToken);

            UpdateStatus(plan);

            if (outcome is not null)
            {
                result.Fail(string.Empty, outcome);
            }

            return result;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<InstallmentPlanResult> ProcessAsync(string planId, bool waitForLock = true, CancellationToken cancellationToken = default)
        => MutateAsync(planId, waitForLock, async plan =>
        {
            if (plan.Status == InstallmentPlanStatus.Draft)
            {
                if (!await TryActivateAsync(plan, cancellationToken) && !string.IsNullOrEmpty(plan.SourceTransactionId))
                {
                    await CloseAbandonedConversionAsync(plan, cancellationToken);
                }

                return InstallmentPlanResult.Success(plan);
            }

            if (plan.Status is InstallmentPlanStatus.Completed or InstallmentPlanStatus.Canceled)
            {
                return InstallmentPlanResult.Success(plan);
            }

            await SyncPaymentsAsync(plan, cancellationToken);

            var now = _clock.UtcNow;

            foreach (var payment in plan.Payments.Where(payment => payment.Number > 0).OrderBy(payment => payment.Number))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (payment.Status is InstallmentPaymentStatus.Paid or InstallmentPaymentStatus.Canceled || payment.DueUtc > now)
                {
                    continue;
                }

                if (plan.CollectionMethod == InstallmentCollectionMethod.Invoice || plan.PaymentMethod is null)
                {
                    await MakeDueAsync(plan, payment, cancellationToken);

                    continue;
                }

                // A failed payment with no retry left belongs to the customer now; it is not charged again unless an
                // administrator asks.
                if (payment.Status == InstallmentPaymentStatus.Failed && payment.NextChargeAttemptUtc is null)
                {
                    continue;
                }

                if (payment.NextChargeAttemptUtc is DateTime next && next > now)
                {
                    continue;
                }

                await ChargePaymentAsync(plan, payment, manual: false, cancellationToken);
            }

            UpdateStatus(plan);

            return InstallmentPlanResult.Success(plan);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<InstallmentPlanResult> CancelAsync(string planId, string reason, CancellationToken cancellationToken = default)
        => MutateAsync(planId, waitForLock: true, async plan =>
        {
            var result = InstallmentPlanResult.Success(plan);

            if (plan.Status is InstallmentPlanStatus.Completed or InstallmentPlanStatus.Canceled)
            {
                return result.Fail(string.Empty, S["The plan is already finished."]);
            }

            await SyncPaymentsAsync(plan, cancellationToken);

            var now = _clock.UtcNow;
            var wasStarted = plan.Status != InstallmentPlanStatus.Draft;

            if (plan.Status == InstallmentPlanStatus.Draft && !string.IsNullOrEmpty(plan.DownPaymentSessionId))
            {
                // Nothing has been received yet; the down payment checkout is closed so a confirmation still in
                // flight cannot settle a plan that no longer exists.
                await _checkoutEngine.CancelAsync(plan.DownPaymentSessionId, "The installment plan was canceled.", cancellationToken);
            }

            foreach (var payment in plan.Payments.Where(payment => payment.Status is not InstallmentPaymentStatus.Paid and not InstallmentPaymentStatus.Canceled))
            {
                payment.Status = InstallmentPaymentStatus.Canceled;
                payment.NextChargeAttemptUtc = null;

                var transaction = await FindTransactionAsync(payment, cancellationToken);

                if (transaction is not null && transaction.Status is TransactionStatus.Pending or TransactionStatus.Outstanding or TransactionStatus.Failed)
                {
                    transaction.Status = TransactionStatus.Canceled;
                    transaction.AutoCollection = null;
                    transaction.UpdatedUtc = now;
                    transaction.Events.Add(new TransactionEvent
                    {
                        CreatedUtc = now,
                        Type = TransactionEventType.Canceled,
                        Message = S["Canceled because the installment plan was canceled."].Value,
                        Reason = string.IsNullOrWhiteSpace(reason)
                            ? S["The installment plan was canceled."].Value
                            : reason,
                        ActorId = CurrentUserId(),
                        ActorName = CurrentUserName(),
                    });

                    await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
                }
            }

            plan.Status = InstallmentPlanStatus.Canceled;
            plan.CanceledUtc = now;

            AddEvent(
                plan,
                InstallmentPlanEventType.Canceled,
                string.IsNullOrWhiteSpace(reason) ? S["The plan was canceled."].Value : S["The plan was canceled: {0}", reason].Value,
                actor: CurrentUserName());

            // A customer whose plan had started is told it stopped, so a payment they expected not to be taken is
            // not a surprise. A draft never took anything from them, so there is nothing to tell.
            if (wasStarted)
            {
                await NotifyCustomerAsync(
                    plan,
                    S["Your payment plan was canceled"].Value,
                    S["Your payment plan {0} was canceled. {1} of {2} was received and stays received; no further payments will be taken.", plan.Title, Format(plan.AmountPaid, plan.Currency), Format(plan.TotalAmount, plan.Currency)].Value,
                    cancellationToken);
            }

            return result;
        }, cancellationToken);

    // Runs a change to one plan under its lock and commits it before the lock is released, so the next holder
    // reads what this one wrote.
    private async Task<InstallmentPlanResult> MutateAsync(
        string planId,
        bool waitForLock,
        Func<InstallmentPlan, Task<InstallmentPlanResult>> mutate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(planId);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            SubscriptionConstants.InstallmentPlans.LockPrefix + planId,
            waitForLock ? _lockTimeout : DistributedLockExtensions.NoWaitTimeout,
            _lockExpiration);

        if (!locked)
        {
            // The plan is not loaded here. When the holder of the lock is this very request (a payment handler
            // reacting to a charge the plan is making), the checkout has committed in between, so loading the plan
            // again would put a second copy of it in the session and the holder's save would then be refused.
            var busy = new InstallmentPlanResult();

            return waitForLock
                ? busy.Fail(string.Empty, S["The plan is being updated by another process. Try again in a moment."])
                : busy;
        }

        await using var _ = locker;

        var plan = await _planStore.FindByIdAsync(planId, cancellationToken);

        if (plan is null)
        {
            return new InstallmentPlanResult().Fail(string.Empty, S["The installment plan could not be found."]);
        }

        var result = await mutate(plan);

        await _planStore.UpdateAsync(plan, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        result.Plan ??= plan;

        return result;
    }

    // Starts the schedule once the first payment is in. Returns whether the plan is active afterwards.
    private async Task<bool> TryActivateAsync(InstallmentPlan plan, CancellationToken cancellationToken)
    {
        // The down payment, or the first scheduled payment of a plan without one: whichever was taken while the card
        // was entered.
        var firstPayment = plan.FirstPayment;

        if (firstPayment is null)
        {
            return false;
        }

        var firstTransaction = await FindTransactionAsync(firstPayment, cancellationToken);

        if (firstTransaction is not null && firstTransaction.Status != TransactionStatus.Paid)
        {
            // The card may have been charged while recording it was interrupted; that money counts.
            await ApplyTakenChargesAsync(firstTransaction, cancellationToken);
        }

        if (firstTransaction?.Status != TransactionStatus.Paid)
        {
            return false;
        }

        var now = _clock.UtcNow;
        var isDownPayment = firstPayment.Number == 0;

        firstPayment.Status = InstallmentPaymentStatus.Paid;
        firstPayment.PaidUtc = firstTransaction.SettledUtc ?? now;
        firstPayment.NextChargeAttemptUtc = null;

        plan.PaymentMethod = await ReadSavedPaymentMethodAsync(plan, cancellationToken);

        if (plan.CollectionMethod == InstallmentCollectionMethod.AutoCharge && plan.PaymentMethod is null)
        {
            // Without a kept card the schedule still has to be collected. Switching to invoices is better than a
            // plan that silently never charges anybody.
            plan.CollectionMethod = InstallmentCollectionMethod.Invoice;

            AddEvent(
                plan,
                InstallmentPlanEventType.Note,
                isDownPayment
                    ? S["The card used for the down payment could not be kept for later payments, so the scheduled payments will be invoiced to the customer instead."].Value
                    : S["The card used for the first payment could not be kept for later payments, so the remaining payments will be invoiced to the customer instead."].Value);
        }

        var autoCollection = plan.CollectionMethod == InstallmentCollectionMethod.AutoCharge
            ? new TransactionAutoCollection
            {
                ProviderKey = plan.PaymentMethod.ProviderKey,
                PaymentMethodDescription = plan.PaymentMethod.Describe(),
            }
            : null;

        foreach (var payment in plan.Payments.Where(payment => payment.Number > 0 && string.IsNullOrEmpty(payment.TransactionId)))
        {
            var transaction = await CreateTransactionAsync(
                plan,
                payment,
                S["{0} — payment {1} of {2}", plan.Title, payment.Number, plan.InstallmentCount].Value,
                TransactionStatus.Pending,
                autoCollection,
                now);

            payment.TransactionId = transaction.ItemId;
            payment.Status = InstallmentPaymentStatus.Scheduled;
        }

        plan.Status = InstallmentPlanStatus.Active;
        plan.ActivatedUtc = now;

        string activated;

        if (isDownPayment)
        {
            activated = plan.CollectionMethod == InstallmentCollectionMethod.AutoCharge
                ? S["The down payment of {0} was received. {1} will be charged on each due date.", Format(firstPayment.Amount, plan.Currency), plan.PaymentMethod.Describe()].Value
                : S["The down payment of {0} was received. The customer will be invoiced for each payment.", Format(firstPayment.Amount, plan.Currency)].Value;
        }
        else
        {
            activated = plan.CollectionMethod == InstallmentCollectionMethod.AutoCharge
                ? S["The first payment of {0} was received. {1} will be charged for each remaining payment on its due date.", Format(firstPayment.Amount, plan.Currency), plan.PaymentMethod.Describe()].Value
                : S["The first payment of {0} was received. The customer will be invoiced for each remaining payment.", Format(firstPayment.Amount, plan.Currency)].Value;
        }

        AddEvent(plan, InstallmentPlanEventType.Activated, activated, paymentNumber: firstPayment.Number);

        if (!string.IsNullOrEmpty(plan.SourceTransactionId))
        {
            await ReplaceSourceTransactionAsync(plan, cancellationToken);
        }

        return true;
    }

    // The balance the customer chose to pay in installments is now owed through the plan, so the original transaction
    // is canceled rather than left for them to pay a second time.
    private async Task ReplaceSourceTransactionAsync(InstallmentPlan plan, CancellationToken cancellationToken)
    {
        var source = await _transactionManager.FindByIdAsync(plan.SourceTransactionId, cancellationToken);

        if (source is null)
        {
            return;
        }

        if (!IsReplaceable(source))
        {
            // Something else settled or closed it while the first payment was being taken. Nothing is undone
            // automatically, because only a person can tell which payment the customer meant.
            AddEvent(plan, InstallmentPlanEventType.Note, S["The transaction '{0}' this plan replaces was no longer outstanding when the plan started, so it was left as it is. Check that the customer is not paying for it twice.", source.Title].Value);

            return;
        }

        var now = _clock.UtcNow;

        source.Status = TransactionStatus.Canceled;
        source.AutoCollection = null;
        source.UpdatedUtc = now;
        source.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Canceled,
            Message = S["Replaced by the installment plan '{0}', which the customer chose at checkout.", plan.Title].Value,
            ActorId = CurrentUserId(),
            ActorName = CurrentUserName(),
        });

        await _transactionManager.UpdateAsync(source, data: null, cancellationToken);

        AddEvent(plan, InstallmentPlanEventType.Note, S["The transaction '{0}' was canceled because this plan replaces it.", source.Title].Value);
    }

    // A plan made at checkout that never got its first payment is closed once its checkout is gone, or once the
    // balance it was meant to replace was settled some other way. The balance itself is left owed.
    private async Task CloseAbandonedConversionAsync(InstallmentPlan plan, CancellationToken cancellationToken)
    {
        var checkout = string.IsNullOrEmpty(plan.DownPaymentSessionId)
            ? null
            : await _sessionStore.GetAsync(plan.DownPaymentSessionId, cancellationToken);

        var checkoutClosed = checkout is null || checkout.Status is CheckoutSessionStatus.Canceled or CheckoutSessionStatus.Expired or CheckoutSessionStatus.Failed;

        var source = await _transactionManager.FindByIdAsync(plan.SourceTransactionId, cancellationToken);
        var sourceSettled = source is null || !IsReplaceable(source);

        if (!checkoutClosed && !sourceSettled)
        {
            return;
        }

        if (!checkoutClosed)
        {
            if (checkout.Status != CheckoutSessionStatus.Pending)
            {
                // A payment is already with the gateway. It finishes first; the plan starts or closes after that.
                return;
            }

            await _checkoutEngine.CancelAsync(checkout.SessionId, "The balance the installment plan was meant to replace was settled another way.", cancellationToken);
        }

        var now = _clock.UtcNow;

        foreach (var payment in plan.Payments.Where(payment => payment.Status is not InstallmentPaymentStatus.Paid and not InstallmentPaymentStatus.Canceled))
        {
            payment.Status = InstallmentPaymentStatus.Canceled;
            payment.NextChargeAttemptUtc = null;

            var transaction = await FindTransactionAsync(payment, cancellationToken);

            if (transaction is not null && transaction.Status is TransactionStatus.Pending or TransactionStatus.Outstanding or TransactionStatus.Failed)
            {
                transaction.Status = TransactionStatus.Canceled;
                transaction.AutoCollection = null;
                transaction.UpdatedUtc = now;
                transaction.Events.Add(new TransactionEvent
                {
                    CreatedUtc = now,
                    Type = TransactionEventType.Canceled,
                    Message = S["Canceled because the customer did not finish setting up the installment plan."].Value,
                });

                await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
            }
        }

        plan.Status = InstallmentPlanStatus.Canceled;
        plan.CanceledUtc = now;

        AddEvent(
            plan,
            InstallmentPlanEventType.Canceled,
            sourceSettled
                ? S["The plan was closed before it started because the balance it was meant to replace was settled another way."].Value
                : S["The plan was closed before it started because the customer left the checkout without paying. The balance is still owed."].Value);
    }

    // Only a balance nobody has paid any of yet is replaced: a plan splits the whole of it.
    private static bool IsReplaceable(Transaction transaction)
        => (transaction.Status is TransactionStatus.Outstanding or TransactionStatus.Pending or TransactionStatus.Failed) &&
            transaction.AmountPaid <= 0m;

    private static Dictionary<string, object> CopyProperties(IEnumerable<KeyValuePair<string, object>> properties)
        => MergeProperties(properties, null);

    // A later value wins: what the customer chose on top of what the transaction already carried.
    private static Dictionary<string, object> MergeProperties(IEnumerable<KeyValuePair<string, object>> first, IEnumerable<KeyValuePair<string, object>> second)
    {
        var merged = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var pair in first ?? [])
        {
            merged[pair.Key] = pair.Value;
        }

        foreach (var pair in second ?? [])
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private async Task<SavedPaymentMethod> ReadSavedPaymentMethodAsync(InstallmentPlan plan, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(plan.DownPaymentSessionId))
        {
            return null;
        }

        var attempts = await _attemptStore.GetBySessionAsync(plan.DownPaymentSessionId, cancellationToken);

        foreach (var attempt in attempts.Where(attempt => attempt.State == PaymentAttemptState.Succeeded))
        {
            var provider = _savedPaymentMethodProviders.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, attempt.ProviderKey, StringComparison.OrdinalIgnoreCase));

            if (provider is null)
            {
                continue;
            }

            try
            {
                var paymentMethod = await provider.GetSavedPaymentMethodAsync(attempt, cancellationToken);

                if (paymentMethod is not null)
                {
                    return paymentMethod;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read the payment method kept by attempt '{AttemptId}' for installment plan '{PlanId}'.", attempt.ItemId, plan.ItemId);
            }
        }

        return null;
    }

    // Charges one payment through the checkout. Returns null when the charge went through (or is still settling),
    // or the reason it failed.
    private async Task<string> ChargePaymentAsync(InstallmentPlan plan, InstallmentPlanPayment payment, bool manual, CancellationToken cancellationToken)
    {
        var transaction = await FindTransactionAsync(payment, cancellationToken);

        if (transaction is null)
        {
            return S["The payment's transaction could not be found."].Value;
        }

        // Money a previous charge of this payment took is applied before anything else. Recording it can be
        // interrupted after the gateway took it; charging again on the strength of the unrecorded balance would take
        // it twice.
        await ApplyTakenChargesAsync(transaction, cancellationToken);

        if (transaction.OutstandingAmount <= 0m)
        {
            await SyncPaymentsAsync(plan, cancellationToken);

            return null;
        }

        var now = _clock.UtcNow;

        // A charge already handed to the gateway is finished rather than repeated: charging again while the first
        // one is still settling is how a customer gets billed twice.
        var previous = await _sessionStore.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId, cancellationToken: cancellationToken);

        if (previous is not null && previous.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending)
        {
            var pending = await _checkoutEngine.TryCompleteAsync(previous.SessionId, cancellationToken);

            if (pending.IsCompleted || pending.Status == CheckoutCompletionStatus.Pending)
            {
                await SyncPaymentsAsync(plan, cancellationToken);

                return null;
            }
        }

        if (previous is not null && previous.Status == CheckoutSessionStatus.Pending)
        {
            // A session that never got as far as the gateway is closed so it cannot be paid later by accident.
            await _checkoutEngine.CancelAsync(previous.SessionId, "Replaced by a new charge.", cancellationToken);
        }

        var session = await _checkoutEngine.StartAsync(
            new StartCheckoutRequest
            {
                ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                ReferenceId = transaction.ItemId,
                OwnerId = plan.OwnerId,
                Contact = new CheckoutContactInfo
                {
                    DisplayName = plan.CustomerName,
                    Email = plan.CustomerEmail,
                },
            },
            cancellationToken);

        payment.CheckoutSessionIds.Add(session.SessionId);
        payment.ChargeAttempts++;
        payment.LastChargeAttemptUtc = now;

        var outcome = await _checkoutEngine.BeginPaymentAsync(
            session.SessionId,
            new BeginPaymentOptions
            {
                ProviderKey = plan.PaymentMethod.ProviderKey,
                ProviderData = CheckoutPaymentDataKeys.ForOffSessionCharge(plan.PaymentMethod),
            },
            cancellationToken);

        if (outcome.Succeeded)
        {
            var completion = await _checkoutEngine.TryCompleteAsync(session.SessionId, cancellationToken);

            if (completion.IsCompleted || completion.Status == CheckoutCompletionStatus.Pending)
            {
                payment.LastFailureMessage = null;
                payment.NextChargeAttemptUtc = null;

                if (payment.Status != InstallmentPaymentStatus.Paid)
                {
                    payment.Status = InstallmentPaymentStatus.Due;
                }

                await SyncPaymentsAsync(plan, cancellationToken);

                return null;
            }

            return await RecordChargeFailureAsync(plan, payment, transaction, completion.ErrorMessage ?? S["The payment was declined."].Value, declined: true, manual, cancellationToken);
        }

        await _checkoutEngine.CancelAsync(session.SessionId, "The charge was not accepted.", cancellationToken);

        return await RecordChargeFailureAsync(
            plan,
            payment,
            transaction,
            outcome.ProviderErrorMessage ?? outcome.ErrorMessage,
            outcome.Declined,
            manual,
            cancellationToken);
    }

    private async Task<string> RecordChargeFailureAsync(
        InstallmentPlan plan,
        InstallmentPlanPayment payment,
        Transaction transaction,
        string reason,
        bool declined,
        bool manual,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        payment.LastFailureMessage = reason;

        if (!declined)
        {
            // The gateway could not be reached or refused to start: nothing is known about the card, so the
            // charge is tried again soon without using up one of the card's retries.
            payment.ChargeAttempts = Math.Max(0, payment.ChargeAttempts - 1);
            payment.NextChargeAttemptUtc = now.Add(_transientRetryDelay);

            AddEvent(plan, InstallmentPlanEventType.ChargeFailed, S["Payment {0} could not be charged and will be tried again shortly: {1}", payment.Number, reason].Value, payment.Number);

            return reason;
        }

        if (manual && payment.DueUtc.Date > now.Date)
        {
            // Collecting early is a favor, not a deadline: a declined early charge leaves the payment on schedule,
            // so it is still charged (and retried) from its due date, and does not count against the card's retries.
            payment.ChargeAttempts = Math.Max(0, payment.ChargeAttempts - 1);
            payment.NextChargeAttemptUtc = null;

            AddEvent(plan, InstallmentPlanEventType.ChargeFailed, S["Payment {0} was declined when charged early by an administrator ({1}). It will still be charged on {2:d}.", payment.Number, reason, payment.DueUtc].Value, payment.Number, CurrentUserName());

            return reason;
        }

        payment.Status = InstallmentPaymentStatus.Failed;

        var settings = await _siteService.GetSettingsAsync<InstallmentPlanSettings>();
        var retryDelay = manual ? null : settings.GetRetryDelay(payment.ChargeAttempts);

        if (retryDelay is TimeSpan delay)
        {
            payment.NextChargeAttemptUtc = now.Add(delay);

            await MarkCollectionFailedAsync(transaction, reason, cancellationToken);

            AddEvent(plan, InstallmentPlanEventType.ChargeFailed, S["Payment {0} was declined ({1}). It will be charged again on {2:d}.", payment.Number, reason, payment.NextChargeAttemptUtc.Value].Value, payment.Number);

            await NotifyCustomerAsync(
                plan,
                S["We could not take your payment"].Value,
                S["We could not charge {0} for {1} of {2}: {3}. We will try again on {4:d}. If your card has changed, please contact us.", plan.PaymentMethod?.Describe(), plan.Title, Format(payment.Amount, plan.Currency), reason, payment.NextChargeAttemptUtc.Value].Value,
                cancellationToken);
        }
        else
        {
            payment.NextChargeAttemptUtc = null;

            if (!manual)
            {
                // No retries left: the payment becomes an ordinary balance the customer can pay themselves, and the
                // overdue reminders take over from here.
                await HandOverToCustomerAsync(plan, transaction, cancellationToken);

                AddEvent(plan, InstallmentPlanEventType.ChargeFailed, S["Payment {0} was declined ({1}) and no retries are left. It is now due from the customer.", payment.Number, reason].Value, payment.Number);

                await NotifyCustomerAsync(
                    plan,
                    S["Your payment is overdue"].Value,
                    S["We could not charge {0} for {1} of {2}: {3}. Please sign in and pay it from your transactions.", plan.PaymentMethod?.Describe(), plan.Title, Format(payment.Amount, plan.Currency), reason].Value,
                    cancellationToken);
            }
            else
            {
                await MarkCollectionFailedAsync(transaction, reason, cancellationToken);

                AddEvent(plan, InstallmentPlanEventType.ChargeFailed, S["Payment {0} was declined when charged by an administrator: {1}", payment.Number, reason].Value, payment.Number, CurrentUserName());
            }
        }

        return reason;
    }

    // A declined charge of a payment that is still collected from the card, by a retry or by an administrator charging it
    // again: the transaction records that its collection failed. A payment handed to the customer instead becomes
    // outstanding, and a retry that goes through settles it.
    private async Task MarkCollectionFailedAsync(Transaction transaction, string reason, CancellationToken cancellationToken)
    {
        if (transaction.Status != TransactionStatus.Pending)
        {
            return;
        }

        var now = _clock.UtcNow;

        transaction.Status = TransactionStatus.Failed;
        transaction.UpdatedUtc = now;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.StatusChanged,
            Message = S["The saved card could not be charged: {0}", reason].Value,
        });

        await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
    }

    // A payment the customer now pays themselves: the transaction is no longer collected automatically and is
    // outstanding, which is what puts it in front of them and starts the overdue reminders.
    private async Task HandOverToCustomerAsync(InstallmentPlan plan, Transaction transaction, CancellationToken cancellationToken)
    {
        if (transaction.Status is not (TransactionStatus.Pending or TransactionStatus.Failed))
        {
            return;
        }

        var now = _clock.UtcNow;

        transaction.Status = TransactionStatus.Outstanding;
        transaction.AutoCollection = null;
        transaction.UpdatedUtc = now;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.StatusChanged,
            Message = plan.CollectionMethod == InstallmentCollectionMethod.Invoice
                ? S["The payment is now due."].Value
                : S["The saved card could not be charged, so the payment is now due from the customer."].Value,
        });

        await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
    }

    // An invoiced payment that reached its due date: the customer is asked to pay it.
    private async Task MakeDueAsync(InstallmentPlan plan, InstallmentPlanPayment payment, CancellationToken cancellationToken)
    {
        if (payment.Status != InstallmentPaymentStatus.Scheduled)
        {
            return;
        }

        var transaction = await FindTransactionAsync(payment, cancellationToken);

        if (transaction is null)
        {
            return;
        }

        await HandOverToCustomerAsync(plan, transaction, cancellationToken);

        payment.Status = InstallmentPaymentStatus.Due;

        AddEvent(plan, InstallmentPlanEventType.PaymentDue, S["Payment {0} of {1} is now due.", payment.Number, Format(payment.Amount, plan.Currency)].Value, payment.Number);
    }

    // Applies to the transaction whatever any checkout for it already collected and is not on it yet. The attempts
    // are the durable record: they are committed before anything that records the payment elsewhere, so they still
    // show the money when that recording was interrupted.
    private async Task ApplyTakenChargesAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        if (transaction.Status is TransactionStatus.Paid or TransactionStatus.Canceled or TransactionStatus.Refunded)
        {
            return;
        }

        var succeeded = (await _attemptStore.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId, cancellationToken))
            .Where(attempt => attempt.State == PaymentAttemptState.Succeeded)
            .ToArray();

        if (succeeded.Length > 0)
        {
            await _settlementService.ApplyAsync(transaction, succeeded, succeeded[^1].SessionId, cancellationToken);
        }
    }

    // Reads every payment's transaction and records what was received or canceled, however it was paid.
    private async Task SyncPaymentsAsync(InstallmentPlan plan, CancellationToken cancellationToken)
    {
        foreach (var payment in plan.Payments.Where(payment => payment.Status is not InstallmentPaymentStatus.Paid and not InstallmentPaymentStatus.Canceled))
        {
            var transaction = await FindTransactionAsync(payment, cancellationToken);

            if (transaction is null)
            {
                continue;
            }

            await ApplyTakenChargesAsync(transaction, cancellationToken);

            switch (transaction.Status)
            {
                case TransactionStatus.Paid:
                case TransactionStatus.Refunded:
                    payment.Status = InstallmentPaymentStatus.Paid;
                    payment.PaidUtc = transaction.SettledUtc ?? _clock.UtcNow;
                    payment.NextChargeAttemptUtc = null;
                    payment.LastFailureMessage = null;

                    if (payment.Number > 0)
                    {
                        AddEvent(plan, InstallmentPlanEventType.PaymentReceived, S["Payment {0} of {1} was received.", payment.Number, Format(payment.Amount, plan.Currency)].Value, payment.Number);
                    }

                    break;

                case TransactionStatus.Canceled:
                case TransactionStatus.Abandoned:
                    payment.Status = InstallmentPaymentStatus.Canceled;
                    payment.NextChargeAttemptUtc = null;

                    break;
            }
        }
    }

    private void UpdateStatus(InstallmentPlan plan)
    {
        if (plan.Status is InstallmentPlanStatus.Draft or InstallmentPlanStatus.Canceled or InstallmentPlanStatus.Completed)
        {
            return;
        }

        var now = _clock.UtcNow;
        var open = plan.Payments.Where(payment => payment.Status is not InstallmentPaymentStatus.Paid and not InstallmentPaymentStatus.Canceled).ToArray();

        if (open.Length == 0)
        {
            plan.Status = InstallmentPlanStatus.Completed;
            plan.CompletedUtc = now;

            AddEvent(plan, InstallmentPlanEventType.Completed, S["Every payment was received. The plan is complete."].Value);

            return;
        }

        // Behind means a charge failed, or a payment the customer owes is past its due date. A payment due today is
        // not behind yet.
        var behind = open.Any(payment =>
            payment.Status == InstallmentPaymentStatus.Failed ||
            (payment.Status == InstallmentPaymentStatus.Due && payment.DueUtc.Date < now.Date));

        plan.Status = behind ? InstallmentPlanStatus.PastDue : InstallmentPlanStatus.Active;
    }

    private async Task<Transaction> CreateTransactionAsync(
        InstallmentPlan plan,
        InstallmentPlanPayment payment,
        string title,
        TransactionStatus status,
        TransactionAutoCollection autoCollection,
        DateTime now)
    {
        var transaction = await _transactionManager.NewAsync();

        transaction.Title = title;
        transaction.Source = SubscriptionConstants.InstallmentPlans.TransactionSource;
        transaction.OwnerId = plan.OwnerId;
        transaction.OwnerKind = CustomerOwnerKind.Authenticated;
        transaction.ReferenceType = SubscriptionConstants.InstallmentPlans.ReferenceType;
        transaction.ReferenceId = plan.ItemId;

        // Every payment names the record the plan pays for, so the module that owns it finds its record from any
        // payment without keeping a map of its own.
        transaction.ParentReferenceType = plan.ReferenceType;
        transaction.ParentReferenceId = plan.ReferenceId;
        transaction.Properties = CopyProperties(plan.Properties);
        transaction.ObligationId = payment.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        transaction.Currency = plan.Currency;
        transaction.Amount = payment.Amount;
        transaction.TaxAmount = 0m;
        transaction.TotalAmount = payment.Amount;
        transaction.AmountPaid = 0m;
        transaction.Status = status;
        transaction.CreatedUtc = now;
        transaction.UpdatedUtc = now;
        transaction.DueUtc = payment.DueUtc;
        transaction.AutoCollection = autoCollection;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Created,
            Message = payment.Number == 0
                ? S["The down payment of installment plan '{0}' was recorded.", plan.Title].Value
                : S["Payment {0} of {1} of installment plan '{2}' was scheduled for {3:d}.", payment.Number, plan.InstallmentCount, plan.Title, payment.DueUtc].Value,
            ActorId = CurrentUserId(),
            ActorName = CurrentUserName(),
        });

        await _transactionManager.CreateAsync(transaction);

        return transaction;
    }

    private Task<Transaction> FindTransactionAsync(InstallmentPlanPayment payment, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(payment.TransactionId)
            ? Task.FromResult<Transaction>(null)
            : _transactionManager.FindByIdAsync(payment.TransactionId, cancellationToken).AsTask();

    private async Task<(IUser User, KeyValuePair<string, string> Error)> ResolveCustomerAsync(CreateInstallmentPlanRequest request)
    {
        if (!string.IsNullOrEmpty(request.CustomerUserId))
        {
            var existing = await _userService.GetUserByUniqueIdAsync(request.CustomerUserId);

            return existing is null
                ? (null, new KeyValuePair<string, string>(nameof(request.CustomerUserId), S["The selected customer could not be found."]))
                : (existing, default);
        }

        var name = request.NewCustomerName?.Trim();
        var email = request.NewCustomerEmail?.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email))
        {
            return (null, new KeyValuePair<string, string>(nameof(request.CustomerUserId), S["Choose a customer, or enter a new customer's name and email."]));
        }

        if (!new EmailAddressAttribute().IsValid(email))
        {
            return (null, new KeyValuePair<string, string>(nameof(request.NewCustomerEmail), S["Enter a valid email address."]));
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return (null, new KeyValuePair<string, string>(nameof(request.NewCustomerEmail), S["A customer with this email already exists. Choose them from the list instead."]));
        }

        // The account has no password. The customer sets one with "Forgot password" when they want to sign in to
        // see or pay their payments; until then nothing can sign in to it.
        var user = new User
        {
            UserName = await CreateUserNameAsync(email),
            Email = email,
            EmailConfirmed = false,
            IsEnabled = true,
        };

        var created = await _userManager.CreateAsync(user);

        if (!created.Succeeded)
        {
            var message = string.Join(" ", created.Errors.Select(error => error.Description));

            return (null, new KeyValuePair<string, string>(nameof(request.NewCustomerEmail), S["The customer could not be created: {0}", message]));
        }

        return (user, default);
    }

    // A user name is derived from the email, because sites restrict which characters a user name may contain (often
    // to letters and digits) and an email rarely fits. A taken name gets a number until it is free.
    private async Task<string> CreateUserNameAsync(string email)
    {
        var allowed = _userManager.Options?.User?.AllowedUserNameCharacters;
        var localPart = email.Split('@')[0];

        var baseName = string.IsNullOrEmpty(allowed)
            ? localPart
            : new string(localPart.Where(allowed.Contains).ToArray());

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "customer";
        }

        var candidate = baseName;

        for (var suffix = 2; await _userManager.FindByNameAsync(candidate) is not null; suffix++)
        {
            candidate = baseName + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private IEnumerable<ICheckoutPaymentProvider> GetSavedPaymentMethodProviders()
        => _providerResolver.GetProviders().Where(provider =>
            provider.Capabilities.SupportsSavedPaymentMethods &&
            provider.Capabilities.SupportsEmbeddedElements &&
            _savedPaymentMethodProviders.Any(saved => string.Equals(saved.Key, provider.Key, StringComparison.OrdinalIgnoreCase)));

    private async Task NotifyCustomerAsync(InstallmentPlan plan, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(plan.OwnerId))
        {
            return;
        }

        try
        {
            await _ownerNotifier.NotifyAsync(
                CustomerOwner.ForUser(plan.OwnerId),
                guestContact: null,
                new CustomerOwnerMessage
                {
                    Subject = subject,
                    Summary = body,
                    TextBody = body,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not notify the customer of installment plan '{PlanId}'.", plan.ItemId);
        }
    }

    private void AddEvent(InstallmentPlan plan, InstallmentPlanEventType type, string message, int? paymentNumber = null, string actor = null)
        => plan.Events.Add(new InstallmentPlanEvent
        {
            CreatedUtc = _clock.UtcNow,
            Type = type,
            Message = message,
            PaymentNumber = paymentNumber,
            ActorName = actor,
        });

    private string CurrentUserId()
        => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    private string CurrentUserName()
        => _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    private static string Format(decimal amount, string currency)
        => $"{currency} {CurrencyScale.Format(amount, currency)}";
}
