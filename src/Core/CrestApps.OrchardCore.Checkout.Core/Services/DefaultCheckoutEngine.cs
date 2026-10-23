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
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

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
    private readonly IEnumerable<ICheckoutPaymentGate> _paymentGates;
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
    /// <param name="paymentGates">The gates that hold what a purchase needs before it is paid for, such as a coupon's use.</param>
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
        ILogger<DefaultCheckoutEngine> logger,
        IEnumerable<ICheckoutPaymentGate> paymentGates = null)
    {
        _sessionStore = sessionStore;
        _attemptStore = attemptStore;
        _providerResolver = providerResolver;
        _recurringProviderResolver = recurringProviderResolver;
        _reconciliationService = reconciliationService;
        _refundService = refundService;
        _handlers = handlers;
        _paymentGates = paymentGates ?? [];
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CheckoutSession> StartAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (referenceType, referenceId, references) = GetReferences(request);

        ArgumentException.ThrowIfNullOrEmpty(referenceType, nameof(request));

        // A return URL is rendered as a link on pages a guest can reach, so one that leads off the site would turn
        // the checkout into an open redirect. It is refused before anything is created.
        if (!string.IsNullOrWhiteSpace(request.ReturnUrl) && !CheckoutReturnInfo.IsLocalUrl(request.ReturnUrl.Trim()))
        {
            throw new ArgumentException($"The return URL '{request.ReturnUrl}' is not a local URL. Only a path on this site can be used as a checkout's return URL.", nameof(request));
        }

        var returnInfo = CreateReturnInfo(request);

        var session = await _sessionStore.NewAsync(
            referenceType,
            referenceId,
            references.Count > 0 ? null : request.ReferenceVersionId,
            newSession =>
            {
                // Before activation, because the handler of the reference type reads them to build what is paid for.
                foreach (var reference in references)
                {
                    newSession.References.Add(reference);
                }

                // Both are seeded before the flow is built, because both change what the checkout contains.
                // The chosen price decides what is charged and how often; a guest's contact details are the
                // only way a completed guest purchase can be receipted or chased.
                if (request.PriceSelection is not null)
                {
                    newSession.Put(request.PriceSelection);
                }

                if (request.Contact is not null && !string.IsNullOrEmpty(request.Contact.Email))
                {
                    newSession.Put(request.Contact);
                }

                if (returnInfo is not null)
                {
                    newSession.Put(returnInfo);
                }

                // A session started for someone else belongs to them. Anything the store derived from the
                // current request (an administrator's identity, or a guest token issued to the browser that
                // happens to be making the call) does not describe this buyer, so it is dropped.
                if (!string.IsNullOrEmpty(request.OwnerId))
                {
                    newSession.OwnerId = request.OwnerId;
                    newSession.GuestTokenHash = null;
                    newSession.IPAddress = null;
                    newSession.AgentInfo = null;
                }
            },
            cancellationToken);

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

            // A provider the store turned off, or one this checkout can't use, is refused here too: the payment step
            // leaves it out, but a request can name any provider.
            if (provider is null || !await CheckoutPaymentMethodSelector.IsAvailableAsync(provider, session, cancellationToken))
            {
                return PaymentBeginOutcome.Failure("The selected payment method is not available.");
            }

            // What the purchase needs is held before any provider is contacted, so a buyer who can't have it, such as
            // a promotion code's last use, is told before paying rather than charged for something already gone.
            var gate = await RunPaymentGatesAsync(session, options.ProviderKey, cancellationToken);

            if (gate.IsRefused)
            {
                var refused = PaymentBeginOutcome.Failure(gate.ErrorMessage);

                refused.InvoiceChanged = gate.InvoiceChanged;

                return refused;
            }

            // A gate may have changed the invoice (it saves the session when it does), so it is read again.
            session.TryGet(out invoice);

            var expectedObligations = CheckoutObligations.GetExpectedObligationIds(invoice);

            if (expectedObligations.Count == 0)
            {
                // Nothing is owed, so there is nothing to begin. The checkout can complete directly.
                return new PaymentBeginOutcome { Succeeded = true };
            }

            if (!TryValidateCapabilities(provider, expectedObligations, session.SettlesExistingBalance, session.SavePaymentMethod, out var capabilityError))
            {
                return PaymentBeginOutcome.Failure(capabilityError);
            }

            if (session.SavePaymentMethod)
            {
                // The session, not the browser, decides that the card is kept: the remaining payments of what the
                // customer agreed to are charged to it without them present.
                options = WithSavedPaymentMethod(options);
            }

            var existingAttempts = (await _attemptStore.GetBySessionAsync(sessionId, cancellationToken)).ToList();

            var outcome = new PaymentBeginOutcome { Succeeded = true };

            foreach (var obligationId in expectedObligations)
            {
                var (step, providerError, declined) = await BeginObligationAsync(
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

                    var failure = PaymentBeginOutcome.Failure(declined
                        ? "The payment was declined. Please try again or choose another payment method."
                        : "The payment could not be started. Please try again or choose another payment method.");

                    failure.ProviderErrorMessage = providerError;
                    failure.Declined = declined;

                    return failure;
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

            // A checkout with nothing to pay never begins a payment, so what it needs is held here instead, before it
            // completes.
            if (expectedObligations.Count == 0)
            {
                var gate = await RunPaymentGatesAsync(session, providerKey: null, cancellationToken);

                if (gate.IsRefused)
                {
                    return new CheckoutCompletionResult
                    {
                        Status = CheckoutCompletionStatus.Blocked,
                        BlockingStepKey = CheckoutConstants.PaymentStepKey,
                        ErrorMessage = gate.ErrorMessage,
                    };
                }

                session.TryGet(out invoice);
                expectedObligations = CheckoutObligations.GetExpectedObligationIds(invoice);

                if (expectedObligations.Count == 0)
                {
                    await SupersedeUnexpectedAttemptsAsync(session, expectedObligations, cancellationToken);
                }
            }

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

    // Gives each gate its say. A gate that refuses and changed the invoice has the session saved, so the buyer sees
    // the new total; anything the gates wrote is committed while the session's lock is still held.
    private async Task<CheckoutPaymentBeginningContext> RunPaymentGatesAsync(CheckoutSession session, string providerKey, CancellationToken cancellationToken)
    {
        var context = new CheckoutPaymentBeginningContext(new CheckoutFlow(session), providerKey);

        foreach (var gate in _paymentGates)
        {
            try
            {
                await gate.BeginningAsync(context, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "The payment gate '{Gate}' failed for checkout '{SessionId}'.", gate.GetType().Name, session.SessionId);

                context.Refuse("The payment could not be started. Please try again.");
            }

            if (context.IsRefused)
            {
                break;
            }
        }

        if (context.InvoiceChanged)
        {
            session.ModifiedUtc = _clock.UtcNow;

            await _sessionStore.SaveAsync(session, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        return context;
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

            // The cancellation is durable before the handlers hear of it, and a handler that fails can't undo it;
            // whatever they let go is committed with it, still inside the lock.
            await _handlers.InvokeAsync((handler, context) => handler.CanceledAsync(context), new CheckoutFlowCanceledContext(new CheckoutFlow(session), reason), _logger);
            await CommitHandlerWorkAsync(session, cancellationToken);

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

            await _handlers.InvokeAsync((handler, context) => handler.ExpiredAsync(context), new CheckoutFlowExpiredContext(new CheckoutFlow(session)), _logger);
            await CommitHandlerWorkAsync(session, cancellationToken);

            return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Canceled };
        }
    }

    // What the checkout pays for: one reference, or several of one type. A list of one is the single reference, so that
    // checkout is exactly the one a caller naming the reference directly gets.
    private static (string ReferenceType, string ReferenceId, List<CheckoutReference> References) GetReferences(StartCheckoutRequest request)
    {
        if (request.References is not { Count: > 0 })
        {
            return (request.ReferenceType, request.ReferenceId, []);
        }

        if (!string.IsNullOrEmpty(request.ReferenceId))
        {
            throw new ArgumentException("A checkout names what it pays for either by its reference id or by its references, not both.", nameof(request));
        }

        var references = new List<CheckoutReference>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var referenceType = request.ReferenceType;

        foreach (var reference in request.References)
        {
            if (reference is null || string.IsNullOrEmpty(reference.ReferenceType) || string.IsNullOrEmpty(reference.ReferenceId))
            {
                throw new ArgumentException("Every reference of a checkout needs a reference type and a reference id.", nameof(request));
            }

            if (string.IsNullOrEmpty(referenceType))
            {
                referenceType = reference.ReferenceType;
            }
            else if (!string.Equals(referenceType, reference.ReferenceType, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Every reference of a checkout must be of the same reference type.", nameof(request));
            }

            if (!ids.Add(reference.ReferenceId))
            {
                throw new ArgumentException($"The reference '{reference.ReferenceId}' appears more than once. A checkout pays for each thing once.", nameof(request));
            }

            references.Add(new CheckoutReference
            {
                ReferenceType = referenceType,
                ReferenceId = reference.ReferenceId,
            });
        }

        return references.Count == 1
            ? (referenceType, references[0].ReferenceId, [])
            : (referenceType, null, references);
    }

    // Captures where the customer returns to and what the confirmation is headed, or null when the caller set none.
    private static CheckoutReturnInfo CreateReturnInfo(StartCheckoutRequest request)
    {
        var returnUrl = string.IsNullOrWhiteSpace(request.ReturnUrl) ? null : request.ReturnUrl.Trim();
        var returnText = string.IsNullOrWhiteSpace(request.ReturnText) ? null : request.ReturnText.Trim();
        var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();

        if (returnUrl is null && returnText is null && title is null)
        {
            return null;
        }

        return new CheckoutReturnInfo
        {
            ReturnUrl = returnUrl,
            ReturnText = returnText,
            Title = title,
        };
    }

    // Begins (or resumes) one obligation. An obligation that already has a non-failed attempt is resumed
    // rather than re-created, which is what makes a refreshed payment page or a double submit safe.
    private async Task<(PaymentBeginStep Step, string ProviderError, bool Declined)> BeginObligationAsync(
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

        // An attempt begun for a different total, before the buyer applied a gift card or a code, is not resumed: it
        // would charge what the invoice no longer asks for. It's set aside, and a new one is made for the new total.
        if (attempt?.State is PaymentAttemptState.Created or PaymentAttemptState.Pending &&
            !MatchesInvoice(attempt, invoice) &&
            await TrySupersedeAsync(session, attempt, cancellationToken))
        {
            existingAttempts.Remove(attempt);
            attempt = null;
        }

        if (attempt?.State == PaymentAttemptState.Succeeded)
        {
            // Already paid. Nothing for the client to do for this obligation.
            return (new PaymentBeginStep
            {
                ObligationId = obligationId,
                AttemptId = attempt.ItemId,
                RequiresAction = false,
            }, null, false);
        }

        if (attempt is null)
        {
            attempt = CreateAttempt(session, invoice, provider.Key, obligationId);

            // A declined attempt is final, so a new one for the same obligation is a genuinely new charge. It
            // needs its own idempotency key: reusing the declined attempt's key would make the gateway replay the
            // decline instead of trying the payment method it is now given.
            var declinedBefore = existingAttempts.Count(candidate =>
                string.Equals(candidate.ObligationId, obligationId, StringComparison.Ordinal) &&
                string.Equals(candidate.ProviderKey, provider.Key, StringComparison.OrdinalIgnoreCase) &&
                candidate.State == PaymentAttemptState.Failed);

            if (declinedBefore > 0)
            {
                attempt.IdempotencyKey += "_retry" + declinedBefore;
            }

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

                        // The same client data the recurring path gets. A checkout that carries both a
                        // one-time and a recurring obligation confirms both with the one payment method the
                        // browser tokenized, and the provider can only honor that if it can see it.
                        ProviderData = options.ProviderData,
                        ReturnUrl = options.ReturnUrl,
                        CancelUrl = options.CancelUrl,
                    },
                    cancellationToken)
                : await BeginRecurringAsync(session, invoice, provider, attempt, interval, options, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Provider '{ProviderKey}' threw while beginning obligation '{ObligationId}' of checkout '{SessionId}'.", provider.Key, obligationId, session.SessionId);

            return (null, null, false);
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Provider '{ProviderKey}' refused to begin obligation '{ObligationId}' of checkout '{SessionId}': {Error}", provider.Key, obligationId, session.SessionId, result.ErrorMessage);

            if (result.Declined)
            {
                // The gateway refused the money, so this attempt is over. Recording that, with the gateway's
                // reason and reference, keeps the ledger truthful and stops a later begin from resuming an
                // attempt that can never settle.
                attempt.State = PaymentAttemptState.Failed;
                attempt.FailureReason = result.ErrorMessage;

                if (!string.IsNullOrEmpty(result.ProviderReference))
                {
                    attempt.ProviderReference = result.ProviderReference;
                }

                await _attemptStore.UpdateAsync(attempt, cancellationToken);
            }

            return (null, result.ErrorMessage, result.Declined);
        }

        // Record the provider's reference immediately so the remote resource can always be found again,
        // even if the rest of this checkout fails.
        attempt.ProviderReference = result.ProviderReference;
        attempt.State = PaymentAttemptState.Pending;

        await _attemptStore.UpdateAsync(attempt, cancellationToken);

        return (new PaymentBeginStep
        {
            ObligationId = obligationId,
            AttemptId = attempt.ItemId,
            ClientSecret = result.ClientSecret,
            RedirectUrl = result.RedirectUrl,
            RequiresAction = result.RequiresAction || !string.IsNullOrEmpty(result.RedirectUrl),
        }, null, false);
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
                CycleAmount = CheckoutObligations.GetCycleAmount(lineItems, invoice.Currency),
                FirstCycleAmount = attempt.ExpectedAmount,
                ProviderData = options.ProviderData,
                ReturnUrl = options.ReturnUrl,
                CancelUrl = options.CancelUrl,
            },
            cancellationToken);
    }

    // Maps an obligation id back to the billing interval it settles, or null when it is the one-time amount.
    private static BillingDurationKey TryGetRecurringInterval(CheckoutInvoice invoice, string obligationId)
        => CheckoutObligations.TryGetRecurringGroup(invoice, obligationId, out var interval, out _) ? interval : null;

    private static bool MatchesInvoice(PaymentAttempt attempt, CheckoutInvoice invoice)
    {
        var (amount, taxAmount) = GetObligationAmounts(invoice, attempt.ObligationId);

        return attempt.ExpectedAmount == amount && attempt.ExpectedTaxAmount == taxAmount;
    }

    // An attempt the provider was already given is only set aside when the provider says nothing was paid, so a
    // payment the buyer finished in another tab is never dropped.
    private async Task<bool> TrySupersedeAsync(CheckoutSession session, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        if (attempt.State == PaymentAttemptState.Pending && _providerResolver.GetProvider(attempt.ProviderKey) is { } provider)
        {
            try
            {
                var verification = await provider.VerifyAsync(new VerifyPaymentContext { Session = session, Attempt = attempt }, cancellationToken);

                if (verification.Status == PaymentStatus.Succeeded)
                {
                    _logger.LogWarning("Attempt '{AttemptId}' of checkout '{SessionId}' was paid before the total changed, so it stands.", attempt.ItemId, session.SessionId);

                    return false;
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not check attempt '{AttemptId}' of checkout '{SessionId}' before setting it aside, so it stands.", attempt.ItemId, session.SessionId);

                return false;
            }
        }

        await SupersedeAsync(attempt, cancellationToken);

        return true;
    }

    private async Task SupersedeAsync(PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        attempt.State = PaymentAttemptState.Canceled;
        attempt.IsSuperseded = true;
        attempt.FailureReason = "The total changed before it was paid.";

        await _attemptStore.UpdateAsync(attempt, cancellationToken);
    }

    // Attempts begun for obligations the invoice no longer has, such as a card payment begun before a gift card was
    // applied that now pays everything, are set aside, so they neither keep the checkout waiting nor get charged.
    private async Task SupersedeUnexpectedAttemptsAsync(CheckoutSession session, IReadOnlyCollection<string> expectedObligations, CancellationToken cancellationToken)
    {
        foreach (var attempt in await _attemptStore.GetBySessionAsync(session.SessionId, cancellationToken))
        {
            if (attempt.State is PaymentAttemptState.Created or PaymentAttemptState.Pending &&
                !attempt.IsSuperseded &&
                !expectedObligations.Contains(attempt.ObligationId))
            {
                await TrySupersedeAsync(session, attempt, cancellationToken);
            }
        }
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

            // Balances such as gift cards are already off the gross; the payment provider carries the tax first, and
            // only what they couldn't leave it is theirs.
            var tax = Math.Min(Math.Max(0m, invoice.TaxAmount - invoice.TenderTaxAmount), gross);

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

            billable.Add((obligationId, CheckoutObligations.GetCycleAmount(group.Value, invoice.Currency)));
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

    // Adds the request to keep the payment method to what the browser sent. Nothing else changes, so the provider still
    // sees the payment method its own script tokenized.
    private static BeginPaymentOptions WithSavedPaymentMethod(BeginPaymentOptions options)
    {
        var providerData = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in options.ProviderData ?? new Dictionary<string, string>())
        {
            providerData[pair.Key] = pair.Value;
        }

        providerData[CheckoutPaymentDataKeys.SavePaymentMethod] = "true";

        return new BeginPaymentOptions
        {
            ProviderKey = options.ProviderKey,
            ReturnUrl = options.ReturnUrl,
            CancelUrl = options.CancelUrl,
            ProviderData = providerData,
        };
    }

    // Refuses a provider that cannot express what the invoice needs, before any money moves. Discovering this
    // after a charge would mean refunding a customer for a configuration mistake.
    //
    // The rule itself lives in CheckoutPaymentMethodSelector, which the payment step also uses to decide which
    // methods to offer. Sharing it is what stops the page from advertising a method this check would reject.
    private static bool TryValidateCapabilities(
        ICheckoutPaymentProvider provider,
        IReadOnlyList<string> obligations,
        bool settlesExistingBalance,
        bool savesPaymentMethod,
        out string error)
    {
        if (CheckoutPaymentMethodSelector.GetEligible([provider], obligations, settlesExistingBalance, savesPaymentMethod).Length > 0)
        {
            error = null;

            return true;
        }

        if (settlesExistingBalance && provider.Capabilities.DefersPayment)
        {
            error = "This balance must be paid now, so the selected payment method can't be used.";

            return false;
        }

        if (savesPaymentMethod && !provider.Capabilities.SupportsSavedPaymentMethods)
        {
            error = "The selected payment method can't keep your card for the later payments.";

            return false;
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
            // The commit before completing detaches the session, so a handler that loads it by id (an order event
            // reading what the checkout carried) would get a second copy, and saving this one would then fail.
            // Tracking it again first hands that handler this same instance.
            await _sessionStore.SaveAsync(session, cancellationToken);

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

        // A failed handler may change the session, for example to stop naming an account it deleted as the owner, and
        // the commit above detached it, so it is saved again.
        await _sessionStore.SaveAsync(session, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

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

    // What the handlers let go of, such as an order and the stock it held, is committed on its own. When it can't be,
    // for example because the same stock changed at the same moment, it is rolled back and left to the sweeps that
    // close abandoned orders and expired holds; the checkout itself is already closed.
    private async Task CommitHandlerWorkAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        try
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (global::YesSql.ConcurrencyException exception)
        {
            await _session.CancelAsync();

            _logger.LogWarning(exception, "What checkout '{SessionId}' held couldn't be let go right away; the sweeps will finish it.", session.SessionId);
        }
    }

    private static bool IsTerminal(CheckoutSessionStatus status)
        => status is CheckoutSessionStatus.Completed
            or CheckoutSessionStatus.Failed
            or CheckoutSessionStatus.Canceled
            or CheckoutSessionStatus.Expired;
}
