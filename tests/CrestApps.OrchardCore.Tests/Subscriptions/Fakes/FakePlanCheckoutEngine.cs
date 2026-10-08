using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Transactions;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Fakes;

/// <summary>
/// A checkout engine for installment plan tests. It records what the plan asked for and, when told the gateway
/// accepted a payment, settles the referenced transaction the way the real settlement handler does.
/// </summary>
internal sealed class FakePlanCheckoutEngine : ICheckoutEngine
{
    private readonly FakeTransactionStore _transactions;
    private readonly InMemoryPaymentAttemptStore _attempts;

    public FakePlanCheckoutEngine(FakeTransactionStore transactions, InMemoryPaymentAttemptStore attempts, InMemoryCheckoutSessionStore sessions)
    {
        _transactions = transactions;
        _attempts = attempts;
        Sessions = sessions;
    }

    public InMemoryCheckoutSessionStore Sessions { get; }

    public List<StartCheckoutRequest> Started { get; } = [];

    public List<(string SessionId, BeginPaymentOptions Options)> Begun { get; } = [];

    public List<string> Canceled { get; } = [];

    /// <summary>
    /// Gets or sets what beginning a payment answers. Defaults to an accepted payment.
    /// </summary>
    public Func<BeginPaymentOptions, PaymentBeginOutcome> OnBegin { get; set; } = _ => new PaymentBeginOutcome { Succeeded = true };

    /// <summary>
    /// Gets or sets what completing a begun payment answers. Defaults to completed.
    /// </summary>
    public CheckoutCompletionStatus CompletionStatus { get; set; } = CheckoutCompletionStatus.Completed;

    public async Task<CheckoutSession> StartAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        Started.Add(request);

        var session = await Sessions.NewAsync(request.ReferenceType, request.ReferenceId, request.ReferenceVersionId, newSession =>
        {
            newSession.OwnerId = request.OwnerId;
            newSession.Status = CheckoutSessionStatus.Pending;
            newSession.CreatedUtc = DateTime.UtcNow.AddTicks(Started.Count);
        }, cancellationToken);

        await Sessions.SaveAsync(session, cancellationToken);

        return session;
    }

    public async Task<PaymentBeginOutcome> BeginPaymentAsync(string sessionId, BeginPaymentOptions options, CancellationToken cancellationToken = default)
    {
        Begun.Add((sessionId, options));

        var outcome = OnBegin(options);

        if (outcome.Succeeded)
        {
            var session = await Sessions.GetAsync(sessionId, cancellationToken);
            session.Status = CheckoutSessionStatus.AwaitingProvider;
        }

        return outcome;
    }

    public async Task<CheckoutCompletionResult> TryCompleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await Sessions.GetAsync(sessionId, cancellationToken);

        if (session is null)
        {
            return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.NotFound };
        }

        if (session.Status == CheckoutSessionStatus.Completed)
        {
            return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.AlreadyCompleted };
        }

        switch (CompletionStatus)
        {
            case CheckoutCompletionStatus.Completed:
                await SettleAsync(session, cancellationToken);
                session.Status = CheckoutSessionStatus.Completed;

                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Completed };

            case CheckoutCompletionStatus.Pending:
                session.Status = CheckoutSessionStatus.PaymentPending;

                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Pending };

            default:
                session.Status = CheckoutSessionStatus.Failed;

                return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Failed, ErrorMessage = "The payment was declined." };
        }
    }

    public async Task<CheckoutCompletionResult> CancelAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        Canceled.Add(sessionId);

        var session = await Sessions.GetAsync(sessionId, cancellationToken);

        if (session is not null)
        {
            session.Status = CheckoutSessionStatus.Canceled;
        }

        return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Canceled };
    }

    public Task<CheckoutCompletionResult> ExpireAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Canceled });

    /// <summary>
    /// Marks the transaction a session settles as paid and records the succeeded attempt, as a completed checkout does.
    /// </summary>
    public async Task SettleAsync(CheckoutSession session, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactions.FindByIdAsync(session.ReferenceId, cancellationToken);

        if (transaction is not null)
        {
            transaction.AmountPaid = transaction.TotalAmount;
            transaction.Status = TransactionStatus.Paid;
            transaction.SettledUtc = DateTime.UtcNow;
        }

        var providerKey = Begun.LastOrDefault(begun => begun.SessionId == session.SessionId).Options?.ProviderKey ?? "card";

        await _attempts.CreateAsync(new PaymentAttempt
        {
            ItemId = "attempt-" + session.SessionId,
            SessionId = session.SessionId,
            ProviderKey = providerKey,
            ObligationId = CheckoutObligations.OneTime,
            ProviderReference = "pi_" + session.SessionId,
            State = PaymentAttemptState.Succeeded,
        }, cancellationToken);
    }
}
