using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutEngine"/>. It owns every transition a checkout can make and is the only
/// component that talks to payment providers, the durable attempt ledger, and the reconciliation service.
/// </summary>
/// <remarks>
/// The ordering here is the whole point of the class:
/// <list type="number">
/// <item><description>A durable attempt is written before a provider is contacted, so a crash between the two
/// leaves a record to reconcile instead of an untracked charge.</description></item>
/// <item><description>The provider's reference is stored the moment it is returned, so the remote resource is
/// never orphaned.</description></item>
/// <item><description>Completion asks the provider what really happened rather than trusting a webhook, and
/// only runs the completion handlers once every obligation is confirmed.</description></item>
/// <item><description>When one obligation fails, the ones that already succeeded are compensated, so a
/// customer is never left having paid for half a purchase that was never fulfilled.</description></item>
/// </list>
/// </remarks>
public sealed class DefaultCheckoutEngine : ICheckoutEngine
{
    private const string SessionLockPrefix = "CHECKOUT_SESSION_";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly ICheckoutRecurringPaymentProviderResolver _recurringProviderResolver;
    private readonly ICheckoutReconciliationService _reconciliationService;
    private readonly ICheckoutRefundService _refundService;
    private readonly IEnumerable<ICheckoutHandler> _handlers;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCheckoutEngine"/> class.
    /// </summary>
    /// <param name="sessionStore">The checkout session store.</param>
    /// <param name="attemptStore">The durable payment attempt ledger.</param>
    /// <param name="providerResolver">The payment provider resolver.</param>
    /// <param name="recurringProviderResolver">The resolver for providers that can establish recurring agreements.</param>
    /// <param name="reconciliationService">The service that verifies attempts against their providers.</param>
    /// <param name="refundService">The durable refund service used to compensate settled obligations.</param>
    /// <param name="handlers">The registered checkout handlers.</param>
    /// <param name="distributedLock">The lock used to serialize transitions of one session across nodes.</param>
    /// <param name="session">The YesSql session used to commit a transition while the lock is held.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    /// <param name="logger">The logger.</param>
    public DefaultCheckoutEngine(
        ICheckoutSessionStore sessionStore,
        IPaymentAttemptStore attemptStore,
        ICheckoutPaymentProviderResolver providerResolver,
        ICheckoutRecurringPaymentProviderResolver recurringProviderResolver,
        ICheckoutReconciliationService reconciliationService,
        ICheckoutRefundService refundService,
        IEnumerable<ICheckoutHandler> handlers,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILogger<DefaultCheckoutEngine> logger)
    {
        _sessionStore = sessionStore;
        _attemptStore = attemptStore;
        _providerResolver = providerResolver;
        _recurringProviderResolver = recurringProviderResolver;
        _reconciliationService = reconciliationService;
        _refundService = refundService;
        _handlers = handlers;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CheckoutSession> StartAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.ReferenceType);

        var session = await _sessionStore.NewAsync(
            request.ReferenceType,
            request.ReferenceId,
            request.ReferenceVersionId,
            cancellationToken);

        // A guest has no account to resolve contact details from later, so the contact captured up front is
        // the only way a completed guest purchase can be receipted or chased.
        if (request.Contact is not null && !string.IsNullOrEmpty(request.Contact.Email))
        {
            session.Put(request.Contact);
        }

        await _sessionStore.SaveAsync(session, cancellationToken);

        return session;
    }

    /// <inheritdoc/>
    public async Task<PaymentBeginOutcome> BeginPaymentAsync(string sessionId, BeginPaymentOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.ProviderKey);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(SessionLockPrefix + sessionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            // Another request is already beginning payment for this session. Refusing is safer than racing:
            // two concurrent begins could create two sets of attempts and charge the customer twice.
            return PaymentBeginOutcome.Failure("Your payment is already being processed. Please wait a moment before trying again.");
        }

        await using (locker)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);

            if (session is null)
            {
                return PaymentBeginOutcome.Failure("The checkout could not be found.");
            }

            if (IsTerminal(session.Status))
            {
                return PaymentBeginOutcome.Failure("This checkout has already been finalized.");
            }

            if (!session.TryGet<CheckoutInvoice>(out var invoice))
            {
                return PaymentBeginOutcome.Failure("The checkout has no invoice to pay.");
            }

            var provider = _providerResolver.GetProvider(options.ProviderKey);

            if (provider is null)
            {
                return PaymentBeginOutcome.Failure("The selected payment method is not available.");
            }

            var expectedObligations = CheckoutObligations.GetExpectedObligationIds(invoice);

            if (expectedObligations.Count == 0)
            {
                // Nothing is owed, so there is nothing to begin. The checkout can complete directly.
                return new PaymentBeginOutcome { Succeeded = true };
            }

            if (!TryValidateCapabilities(provider, expectedObligations, out var capabilityError))
            {
                return PaymentBeginOutcome.Failure(capabilityError);
            }

            var existingAttempts = (await _attemptStore.GetBySessionAsync(sessionId, cancellationToken)).ToList();

            var outcome = new PaymentBeginOutcome { Succeeded = true };

            foreach (var obligationId in expectedObligations)
            {
                var step = await BeginObligationAsync(
                    session,
                    invoice,
                    provider,
                    obligationId,
                    existingAttempts,
                    options,
                    cancellationToken);

                if (step is null)
                {
                    // The provider refused. Leave the attempts that were already created in place: they are
                    // durable records of real remote resources and the reconciliation sweep resolves them.
                    // They are committed here, inside the lock, so a retry that starts the instant this
                    // request returns sees them and resumes instead of creating a second set.
                    await _session.SaveChangesAsync(cancellationToken);

                    return PaymentBeginOutcome.Failure("The payment could not be started. Please try again or choose another payment method.");
                }

                outcome.Steps.Add(step);
            }

            session.Status = CheckoutSessionStatus.AwaitingProvider;
            session.ModifiedUtc = _clock.UtcNow;

            await _sessionStore.SaveAsync(session, cancellationToken);

            // Commit while the lock is still held so a concurrent begin observes the attempts this one
            // created instead of creating a second set.
            await _session.SaveChangesAsync(cancellationToken);

            return outcome;
        }
    }

    /// <inheritdoc/>
    public async Task<CheckoutCompletionResult> TryCompleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(SessionLockPrefix + sessionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            // Someone else is completing this checkout right now. Report it as pending so the caller retries
            // rather than showing the customer a failure for a checkout that is about to succeed.
            return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Pending };
        }

        await using (locker)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);

            if (session is null)
            {
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.NotFound };
            }

            if (session.Status == CheckoutSessionStatus.Completed)
            {
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.AlreadyCompleted };
            }

            if (IsTerminal(session.Status))
            {
                return new CheckoutCompletionResult
                {
                    Status = session.Status == CheckoutSessionStatus.Canceled
                        ? CheckoutCompletionStatus.Canceled
                        : CheckoutCompletionStatus.Failed,
                };
            }

            var flow = new CheckoutFlow(session);

            var blockingStep = GetFirstIncompleteStep(session);

            if (blockingStep is not null)
            {
                return new CheckoutCompletionResult
                {
                    Status = CheckoutCompletionStatus.Blocked,
                    BlockingStepKey = blockingStep.Key,
                };
            }

            if (!session.TryGet<CheckoutInvoice>(out var invoice))
            {
                return Failed(session, "The checkout has no invoice.");
            }

            var expectedObligations = CheckoutObligations.GetExpectedObligationIds(invoice);

            var reconciliation = await _reconciliationService.ReconcileAsync(session, expectedObligations, cancellationToken);

            if (reconciliation.FailedObligationIds.Count > 0)
            {
                await CompensateAsync(session, reconciliation.SettledObligationIds, cancellationToken);

                return await FailAsync(session, flow, "The payment was declined.", cancellationToken);
            }

            if (!reconciliation.IsFullySettled)
            {
                var pending = new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Pending };

                foreach (var obligationId in reconciliation.OutstandingObligationIds)
                {
                    pending.OutstandingObligationIds.Add(obligationId);
                }

                // A checkout that was submitted without a payment ever being begun has nothing to wait for:
                // it is still the customer's to act on, so it stays where it is instead of moving to a state
                // that only a provider can move it out of.
                if (!reconciliation.HasAttempts)
                {
                    return pending;
                }

                // The provider has not given an authoritative answer yet. Record that we are waiting and let
                // the caller poll; blocking a request here would tie up a worker per customer and still time
                // out behind a proxy long before a slow provider finished.
                session.Status = CheckoutSessionStatus.PaymentPending;
                session.ModifiedUtc = _clock.UtcNow;

                await _sessionStore.SaveAsync(session, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);

                return pending;
            }

            // Every obligation is confirmed. That fact is committed before anything is fulfilled, so a
            // fulfillment that fails cannot discard the provider's confirmations along with its own writes;
            // the sweep then retries the fulfillment against attempts it can trust.
            if (session.Status != CheckoutSessionStatus.PaymentPending)
            {
                session.Status = CheckoutSessionStatus.PaymentPending;
                session.ModifiedUtc = _clock.UtcNow;

                await _sessionStore.SaveAsync(session, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);

            return await CompleteAsync(session, flow, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<CheckoutCompletionResult> CancelAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(SessionLockPrefix + sessionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Pending };
        }

        await using (locker)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);

            if (session is null)
            {
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.NotFound };
            }

            if (session.Status == CheckoutSessionStatus.Completed)
            {
                // A completed checkout is not cancelable; reversing it is a refund, which is a money movement
                // the caller has to request explicitly through the refund service.
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.AlreadyCompleted };
            }

            if (session.Status == CheckoutSessionStatus.Canceled)
            {
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Canceled };
            }

            await CancelAttemptsAsync(session, reason, cancellationToken);

            session.Status = CheckoutSessionStatus.Canceled;
            session.ModifiedUtc = _clock.UtcNow;

            await _sessionStore.SaveAsync(session, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Canceled };
        }
    }

    /// <inheritdoc/>
    public async Task<CheckoutCompletionResult> ExpireAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(SessionLockPrefix + sessionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Pending };
        }

        await using (locker)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);

            if (session is null)
            {
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.NotFound };
            }

            if (session.Status == CheckoutSessionStatus.Completed)
            {
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.AlreadyCompleted };
            }

            if (IsTerminal(session.Status))
            {
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Canceled };
            }

            // Money that was actually taken is never expired away. A checkout with a confirmed attempt is
            // an unfulfilled purchase, which is the sweep's job to finish, not this method's job to close.
            var attempts = await _attemptStore.GetBySessionAsync(session.SessionId, cancellationToken);

            if (attempts.Any(attempt => attempt.State == PaymentAttemptState.Succeeded))
            {
                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Pending };
            }

            await CancelAttemptsAsync(session, "The checkout expired before it was completed.", cancellationToken);

            session.Status = CheckoutSessionStatus.Expired;
            session.ModifiedUtc = _clock.UtcNow;

            await _sessionStore.SaveAsync(session, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Canceled };
        }
    }

    // Begins (or resumes) one obligation. An obligation that already has a non-failed attempt is resumed
    // rather than re-created, which is what makes a refreshed payment page or a double submit safe.
    private async Task<PaymentBeginStep> BeginObligationAsync(
        CheckoutSession session,
        CheckoutInvoice invoice,
        ICheckoutPaymentProvider provider,
        string obligationId,
        List<PaymentAttempt> existingAttempts,
        BeginPaymentOptions options,
        CancellationToken cancellationToken)
    {
        var attempt = existingAttempts.FirstOrDefault(candidate =>
            string.Equals(candidate.ObligationId, obligationId, StringComparison.Ordinal) &&
            string.Equals(candidate.ProviderKey, provider.Key, StringComparison.OrdinalIgnoreCase) &&
            candidate.State is PaymentAttemptState.Created or PaymentAttemptState.Pending or PaymentAttemptState.Succeeded);

        if (attempt?.State == PaymentAttemptState.Succeeded)
        {
            // Already paid. Nothing for the client to do for this obligation.
            return new PaymentBeginStep
            {
                ObligationId = obligationId,
                AttemptId = attempt.ItemId,
                RequiresAction = false,
            };
        }

        if (attempt is null)
        {
            attempt = CreateAttempt(session, invoice, provider.Key, obligationId);

            // Persist BEFORE the provider is contacted. A crash between these two lines must leave a record
            // that the reconciliation sweep can resolve, not an invisible charge.
            await _attemptStore.CreateAsync(attempt, cancellationToken);
        }

        PaymentBeginResult result;

        try
        {
            // A recurring obligation establishes a billing agreement rather than taking a single charge, so it
            // is routed to the provider's recurring capability. Falling back to the one-time path would create
            // a single charge and silently never bill the customer again.
            var interval = TryGetRecurringInterval(invoice, obligationId);

            result = interval is null
                ? await provider.BeginAsync(
                    new BeginPaymentContext
                    {
                        Session = session,
                        Attempt = attempt,
                        Invoice = invoice,
                        ReturnUrl = options.ReturnUrl,
                        CancelUrl = options.CancelUrl,
                    },
                    cancellationToken)
                : await BeginRecurringAsync(session, invoice, provider, attempt, interval, options, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Provider '{ProviderKey}' threw while beginning obligation '{ObligationId}' of checkout '{SessionId}'.", provider.Key, obligationId, session.SessionId);

            return null;
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Provider '{ProviderKey}' refused to begin obligation '{ObligationId}' of checkout '{SessionId}': {Error}", provider.Key, obligationId, session.SessionId, result.ErrorMessage);

            return null;
        }

        // Record the provider's reference immediately so the remote resource can always be found again,
        // even if the rest of this checkout fails.
        attempt.ProviderReference = result.ProviderReference;
        attempt.State = PaymentAttemptState.Pending;

        await _attemptStore.UpdateAsync(attempt, cancellationToken);

        return new PaymentBeginStep
        {
            ObligationId = obligationId,
            AttemptId = attempt.ItemId,
            ClientSecret = result.ClientSecret,
            RedirectUrl = result.RedirectUrl,
            RequiresAction = result.RequiresAction || !string.IsNullOrEmpty(result.RedirectUrl),
        };
    }

    // Establishes the recurring agreement for one interval. A provider whose capabilities claim recurring
    // support but which ships no recurring implementation is refused here rather than silently taking a single
    // charge for something the customer expects to renew.
    private async Task<PaymentBeginResult> BeginRecurringAsync(
        CheckoutSession session,
        CheckoutInvoice invoice,
        ICheckoutPaymentProvider provider,
        PaymentAttempt attempt,
        BillingDurationKey interval,
        BeginPaymentOptions options,
        CancellationToken cancellationToken)
    {
        var recurringProvider = _recurringProviderResolver.GetProvider(provider.Key);

        if (recurringProvider is null)
        {
            return PaymentBeginResult.Failure($"The payment method '{provider.Key}' cannot set up a recurring payment.");
        }

        invoice.GetRecurringGroups().TryGetValue(interval, out var lineItems);
        lineItems ??= [];

        var deferralDays = CheckoutObligations.GetDeferralDays(lineItems);

        return await recurringProvider.BeginRecurringAsync(
            new BeginRecurringPaymentContext
            {
                Session = session,
                Attempt = attempt,
                Invoice = invoice,
                Interval = interval,
                LineItems = lineItems,

                // A trial and a delayed start both mean the gateway starts billing later. The longest one
                // wins: billing before the trial promised on any line has run out produces a chargeback.
                TrialDays = deferralDays > 0 ? deferralDays : null,

                // The gateway prices the agreement from the full cycle amount, never from what is due now.
                // Pricing it from the attempt would bill a first-cycle discount, or a trial's zero, forever.
                CycleAmount = Money.Round(lineItems.Sum(lineItem => lineItem.GetLineTotal(invoice.Currency)), invoice.Currency),
                FirstCycleAmount = attempt.ExpectedAmount,
                ProviderData = options.ProviderData,
                ReturnUrl = options.ReturnUrl,
                CancelUrl = options.CancelUrl,
            },
            cancellationToken);
    }

    // Maps an obligation id back to the billing interval it settles, or null when it is the one-time amount.
    private static BillingDurationKey TryGetRecurringInterval(CheckoutInvoice invoice, string obligationId)
    {
        if (string.Equals(obligationId, CheckoutObligations.OneTime, StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var key in invoice.GetRecurringGroups().Keys)
        {
            if (string.Equals(CheckoutObligations.Recurring(key), obligationId, StringComparison.Ordinal))
            {
                return key;
            }
        }

        return null;
    }

    private static PaymentAttempt CreateAttempt(CheckoutSession session, CheckoutInvoice invoice, string providerKey, string obligationId)
    {
        var (amount, taxAmount) = GetObligationAmounts(invoice, obligationId);

        var attempt = new PaymentAttempt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = session.SessionId,
            ReferenceType = session.ReferenceType,
            ReferenceId = session.ReferenceId,
            ProviderKey = providerKey,
            ObligationId = obligationId,
            ExpectedAmount = amount,
            ExpectedTaxAmount = taxAmount,
            Currency = invoice.Currency,
            State = PaymentAttemptState.Created,
            TaxSnapshot = invoice.TaxSnapshot,
        };

        // The key is derived from everything that defines the charge, so retrying the same charge collapses at
        // the provider while a genuinely different charge (a changed amount or currency) gets a new key.
        attempt.IdempotencyKey = $"checkout_{session.SessionId}_{obligationId}_{Money.ToMinorUnits(amount + taxAmount, invoice.Currency)}_{invoice.Currency}";

        return attempt;
    }

    // Splits the invoice into the money each obligation is responsible for. Tax is only ever attached to the
    // up-front charge, matching how the tax service folds exclusive tax into the initial payment.
    private static (decimal Amount, decimal TaxAmount) GetObligationAmounts(CheckoutInvoice invoice, string obligationId)
    {
        if (string.Equals(obligationId, CheckoutObligations.OneTime, StringComparison.Ordinal))
        {
            var gross = invoice.InitialPaymentAmount ?? 0m;
            var tax = invoice.TaxAmount;

            // InitialPaymentAmount already includes the exclusive tax folded in by the tax service, so the
            // taxable base is the remainder. Charging gross + tax again would double-charge the tax.
            return (Money.Round(gross - tax, invoice.Currency), tax);
        }

        var firstCycleAmounts = GetFirstCycleAmounts(invoice);

        return firstCycleAmounts.TryGetValue(obligationId, out var amount)
            ? (amount, 0m)
            : (0m, 0m);
    }

    // Allocates what is actually due for the first cycle across the recurring obligations. The invoice's
    // first recurring amount already has any first-cycle discount taken off, so it is shared out in
    // proportion to each group's full cycle amount, with the last group absorbing the rounding. A group
    // that is deferred by a trial or a delayed start is due nothing now, and so expects nothing now: the
    // attempt is what the provider's confirmation is checked against, and a trial that verifies as zero
    // collected must not be refused for falling short of a charge that was never due.
    private static Dictionary<string, decimal> GetFirstCycleAmounts(CheckoutInvoice invoice)
    {
        var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var billable = new List<(string ObligationId, decimal CycleAmount)>();

        foreach (var group in invoice.GetRecurringGroups())
        {
            var obligationId = CheckoutObligations.Recurring(group.Key);

            if (CheckoutObligations.GetDeferralDays(group.Value) > 0)
            {
                amounts[obligationId] = 0m;

                continue;
            }

            billable.Add((obligationId, Money.Round(group.Value.Sum(lineItem => lineItem.GetLineTotal(invoice.Currency)), invoice.Currency)));
        }

        if (billable.Count == 0)
        {
            return amounts;
        }

        var fullTotal = billable.Sum(entry => entry.CycleAmount);
        var dueTotal = Money.Round(invoice.FirstRecurringPaymentAmount ?? fullTotal, invoice.Currency);
        var allocated = 0m;

        for (var i = 0; i < billable.Count; i++)
        {
            var (obligationId, cycleAmount) = billable[i];

            var share = i == billable.Count - 1 || fullTotal <= 0m
                ? Money.Round(dueTotal - allocated, invoice.Currency)
                : Money.Round(dueTotal * (cycleAmount / fullTotal), invoice.Currency);

            share = Math.Max(0m, Math.Min(share, cycleAmount));
            allocated += share;
            amounts[obligationId] = share;
        }

        return amounts;
    }

    // Refuses a provider that cannot express what the invoice needs, before any money moves. Discovering this
    // after a charge would mean refunding a customer for a configuration mistake.
    //
    // The rule itself lives in CheckoutPaymentMethodSelector, which the payment step also uses to decide which
    // methods to offer. Sharing it is what stops the page from advertising a method this check would reject.
    private static bool TryValidateCapabilities(
        ICheckoutPaymentProvider provider,
        IReadOnlyList<string> obligations,
        out string error)
    {
        if (CheckoutPaymentMethodSelector.GetEligible([provider], obligations).Length > 0)
        {
            error = null;

            return true;
        }

        var hasOneTime = obligations.Contains(CheckoutObligations.OneTime);
        var hasRecurring = obligations.Any(id => !string.Equals(id, CheckoutObligations.OneTime, StringComparison.Ordinal));

        error = !hasOneTime || !provider.Capabilities.SupportsOneTimePayments
            ? hasRecurring && !provider.Capabilities.SupportsRecurringPayments
                ? "The selected payment method cannot set up a recurring payment."
                : "The selected payment method cannot collect a one-time payment."
            : "The selected payment method cannot collect an up-front amount and a recurring payment together.";

        return false;
    }

    private async Task<CheckoutCompletionResult> CompleteAsync(CheckoutSession session, CheckoutFlow flow, CancellationToken cancellationToken)
    {
        var completingContext = new CheckoutFlowCompletingContext(flow);

        try
        {
            // Completing handlers are invoked directly rather than through the swallow-and-log helper: a
            // handler that cannot fulfill the purchase must stop the checkout from being marked complete.
            foreach (var handler in _handlers)
            {
                await handler.CompletingAsync(completingContext);
            }

            session.Status = CheckoutSessionStatus.Completed;
            session.CompletedUtc = _clock.UtcNow;
            session.ModifiedUtc = _clock.UtcNow;

            await _sessionStore.SaveAsync(session, cancellationToken);

            // Commit the fulfillment writes and the status transition together, inside the lock, so a
            // concurrent completion sees a completed session instead of fulfilling a second time.
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "A completing handler failed for checkout '{SessionId}'. The payment is confirmed; the fulfillment is retried by the reconciliation sweep.", session.SessionId);

            // Discarding the transaction throws away every write a completing handler made, which is the
            // point: a half-fulfilled purchase must not be committed. Nothing written to this session after
            // the cancel would persist either, so the checkout is deliberately left as the sweep last saw
            // it, with its confirmed attempts intact, rather than marked failed on a write that would be
            // lost. The customer has paid; refunding them for a fulfillment hiccup would be worse than
            // fulfilling a little later.
            await _session.CancelAsync();

            // The in-memory session was mutated by handlers that have now been rolled back.
            session.Status = CheckoutSessionStatus.PaymentPending;

            return new CheckoutCompletionResult
            {
                Status = CheckoutCompletionStatus.Pending,
                ErrorMessage = "The purchase could not be completed. It will be retried shortly.",
            };
        }

        // Completed handlers run after the transition is durable. A failure here (for example a notification
        // that could not be sent) must not undo a paid, fulfilled checkout, so they are invoked through the
        // swallow-and-log helper.
        await _handlers.InvokeAsync((handler, context) => handler.CompletedAsync(context), new CheckoutFlowCompletedContext(flow), _logger);

        return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Completed };
    }

    private async Task<CheckoutCompletionResult> FailAsync(CheckoutSession session, CheckoutFlow flow, string errorMessage, CancellationToken cancellationToken)
    {
        session.Status = CheckoutSessionStatus.Failed;
        session.ModifiedUtc = _clock.UtcNow;

        await _sessionStore.SaveAsync(session, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        await _handlers.InvokeAsync((handler, context) => handler.FailedAsync(context), new CheckoutFlowFailedContext(flow), _logger);

        return new CheckoutCompletionResult
        {
            Status = CheckoutCompletionStatus.Failed,
            ErrorMessage = errorMessage,
        };
    }

    private static CheckoutCompletionResult Failed(CheckoutSession session, string errorMessage)
    {
        session.Status = CheckoutSessionStatus.Failed;

        return new CheckoutCompletionResult
        {
            Status = CheckoutCompletionStatus.Failed,
            ErrorMessage = errorMessage,
        };
    }

    // Returns money already collected for a checkout that cannot complete. Without this a customer whose
    // second obligation was declined would keep paying for the first one forever.
    private async Task CompensateAsync(CheckoutSession session, IEnumerable<string> settledObligationIds, CancellationToken cancellationToken)
    {
        var settled = settledObligationIds.ToHashSet(StringComparer.Ordinal);

        if (settled.Count == 0)
        {
            return;
        }

        var attempts = await _attemptStore.GetBySessionAsync(session.SessionId, cancellationToken);

        foreach (var attempt in attempts)
        {
            if (attempt.State != PaymentAttemptState.Succeeded ||
                string.IsNullOrEmpty(attempt.TransactionId) ||
                !settled.Contains(attempt.ObligationId))
            {
                continue;
            }

            try
            {
                // Refunds go through the durable refund service, never straight to the gateway, so the
                // refund is recorded before it is issued and the gateway's own refund notification
                // correlates to it instead of being quarantined for manual review.
                await _refundService.RequestRefundAsync(
                    new RequestPaymentRefundContext
                    {
                        SessionId = session.SessionId,
                        OriginalTransactionId = attempt.TransactionId,
                        Reason = "The checkout could not be completed, so the settled obligations were returned.",
                    },
                    cancellationToken);
            }
            catch (Exception exception)
            {
                // The customer's money is at stake, so a failure here is loud: an operator has to resolve it.
                _logger.LogError(exception, "Failed to compensate settled obligation '{ObligationId}' of checkout '{SessionId}'. This payment must be refunded manually.", attempt.ObligationId, session.SessionId);
            }
        }
    }

    // Releases remote resources for a checkout the customer abandoned. A provider that reports the
    // cancellation as unconfirmed leaves the attempt pending for the reconciliation sweep rather than being
    // recorded as canceled on our word alone.
    private async Task CancelAttemptsAsync(CheckoutSession session, string reason, CancellationToken cancellationToken)
    {
        var attempts = await _attemptStore.GetBySessionAsync(session.SessionId, cancellationToken);

        foreach (var attempt in attempts)
        {
            if (attempt.State is not (PaymentAttemptState.Created or PaymentAttemptState.Pending))
            {
                continue;
            }

            var provider = _providerResolver.GetProvider(attempt.ProviderKey);

            if (provider is null)
            {
                continue;
            }

            try
            {
                var result = await provider.CancelAsync(
                    new CancelPaymentContext
                    {
                        Session = session,
                        Attempt = attempt,
                        Reason = reason,
                    },
                    cancellationToken);

                if (!result.Succeeded)
                {
                    continue;
                }

                attempt.State = PaymentAttemptState.Canceled;
                attempt.FailureReason = reason;

                await _attemptStore.UpdateAsync(attempt, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to cancel attempt '{AttemptId}' of checkout '{SessionId}'.", attempt.ItemId, session.SessionId);
            }
        }
    }

    private static CheckoutFlowStep GetFirstIncompleteStep(CheckoutSession session)
    {
        foreach (var step in session.Steps)
        {
            if (step.Conceal || !step.CollectData)
            {
                continue;
            }

            if (!session.SavedSteps.ContainsKey(step.Key))
            {
                return step;
            }
        }

        return null;
    }

    private static bool IsTerminal(CheckoutSessionStatus status)
        => status is CheckoutSessionStatus.Completed
            or CheckoutSessionStatus.Failed
            or CheckoutSessionStatus.Canceled
            or CheckoutSessionStatus.Expired;
}
