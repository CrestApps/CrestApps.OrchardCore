using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Finds a person's checkout sessions by their account and by the email they gave, exports the sessions with the
/// payments and refunds taken in them, and anonymizes the sessions.
/// </summary>
/// <remarks>
/// A guest checkout is owned by an id derived from its session, so every guest session found adds a guest owner, and
/// the other modules find the person's guest transactions and subscriptions through it. A user who is not the person
/// is never added as an owner, even when their checkout carries the person's email, because that would sweep up
/// someone else's records.
/// </remarks>
public sealed class CheckoutPersonalDataContributor : ICommercePersonalDataContributor
{
    internal const int PageSize = 100;
    internal const int BatchSize = 50;
    internal const int MaxSessions = 10_000;

    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IPaymentAttemptStore _paymentAttemptStore;
    private readonly IPaymentRefundStore _paymentRefundStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="sessionStore">The checkout session store.</param>
    /// <param name="paymentAttemptStore">The durable payment ledger.</param>
    /// <param name="paymentRefundStore">The durable refund ledger.</param>
    public CheckoutPersonalDataContributor(
        ICheckoutSessionStore sessionStore,
        IPaymentAttemptStore paymentAttemptStore,
        IPaymentRefundStore paymentRefundStore)
    {
        _sessionStore = sessionStore;
        _paymentAttemptStore = paymentAttemptStore;
        _paymentRefundStore = paymentRefundStore;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var userId in subject.UserIds.ToArray())
        {
            await FindAsync(subject, userId, email: null, cancellationToken);
        }

        foreach (var email in subject.Emails.ToArray())
        {
            await FindAsync(subject, ownerId: null, email, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var session in await LoadAsync(context.Subject, cancellationToken))
        {
            session.TryGet<CheckoutContactInfo>(out var contact);

            context.Export.CheckoutSessions.Add(new CommerceCheckoutSessionRecord
            {
                SessionId = session.SessionId,
                ReferenceType = session.ReferenceType,
                ReferenceId = session.ReferenceId,
                Status = session.Status.ToString(),
                Currency = session.Currency,
                OwnerId = session.OwnerId,
                CreatedUtc = session.CreatedUtc,
                CompletedUtc = session.CompletedUtc,
                ContactName = contact?.DisplayName,
                ContactEmail = contact?.Email,
                ContactPhone = contact?.Phone,
                IPAddress = session.IPAddress,
                UserAgent = session.AgentInfo,
                SavedSteps = session.SavedSteps?.DeepClone() as JsonObject,
            });

            foreach (var attempt in (await _paymentAttemptStore.GetBySessionAsync(session.SessionId, cancellationToken)).OrderBy(attempt => attempt.CreatedUtc))
            {
                context.Export.Payments.Add(new CommercePaymentRecord
                {
                    ItemId = attempt.ItemId,
                    CheckoutSessionId = attempt.SessionId,
                    TransactionId = attempt.TransactionId,
                    ProviderKey = attempt.ProviderKey,
                    ProviderReference = attempt.ProviderReference,
                    ReferenceType = attempt.ReferenceType,
                    ReferenceId = attempt.ReferenceId,
                    State = attempt.State.ToString(),
                    Currency = attempt.Currency,
                    Amount = attempt.ConfirmedAmount != 0m ? attempt.ConfirmedAmount : attempt.ExpectedAmount,
                    TaxAmount = attempt.ConfirmedAmount != 0m ? attempt.ConfirmedTaxAmount : attempt.ExpectedTaxAmount,
                    CreatedUtc = attempt.CreatedUtc,
                    UpdatedUtc = attempt.UpdatedUtc,
                });
            }

            foreach (var refund in (await _paymentRefundStore.GetBySessionAsync(session.SessionId, cancellationToken)).OrderBy(refund => refund.CreatedUtc))
            {
                context.Export.Refunds.Add(new CommerceRefundRecord
                {
                    ItemId = refund.ItemId,
                    CheckoutSessionId = refund.SessionId,
                    PaymentId = refund.OriginalAttemptId,
                    ProviderKey = refund.ProviderKey,
                    ProviderReference = refund.ProviderRefundReference,
                    Status = refund.Status.ToString(),
                    Currency = refund.Currency,
                    Amount = refund.RefundGrossAmount,
                    TaxAmount = refund.RefundTaxAmount,
                    Reason = refund.Reason,
                    CreatedUtc = refund.CreatedUtc,
                    CompletedUtc = refund.CompletedUtc,
                });
            }
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var session in await LoadAsync(context.Subject, cancellationToken))
        {
            Anonymize(session, context);

            await _sessionStore.SaveAsync(session, cancellationToken);

            context.Result.CheckoutSessions++;

            foreach (var attempt in await _paymentAttemptStore.GetBySessionAsync(session.SessionId, cancellationToken))
            {
                if (!context.Subject.IsMentionedIn(attempt.FailureReason))
                {
                    continue;
                }

                attempt.FailureReason = context.Scrub(attempt.FailureReason);

                await _paymentAttemptStore.UpdateAsync(attempt, cancellationToken);

                context.Result.Payments++;
            }

            foreach (var refund in await _paymentRefundStore.GetBySessionAsync(session.SessionId, cancellationToken))
            {
                if (!context.Subject.IsMentionedIn(refund.Reason) && !context.Subject.IsMentionedIn(refund.FailureReason))
                {
                    continue;
                }

                refund.Reason = context.Scrub(refund.Reason);
                refund.FailureReason = context.Scrub(refund.FailureReason);

                await _paymentRefundStore.UpdateAsync(refund, cancellationToken);

                context.Result.Refunds++;
            }
        }
    }

    /// <summary>
    /// Removes the buyer's contact, IP address, user agent and saved steps from a session, and the person's emails and
    /// names from anything else its property bag holds. The amounts, invoice, status and dates stay. The modification
    /// time is left alone, so a pending session is not kept from the abandoned-checkout sweep.
    /// </summary>
    internal static void Anonymize(CheckoutSession session, CommercePersonalDataAnonymizeContext context)
    {
        if (session.TryGet<CheckoutContactInfo>(out var contact) && contact is not null)
        {
            session.Put(new CheckoutContactInfo
            {
                DisplayName = context.ReplaceName(contact.DisplayName),
                BillingAddress = contact.BillingAddress is null
                    ? null
                    : new Address
                    {
                        Country = contact.BillingAddress.Country,
                        Region = contact.BillingAddress.Region,
                    },
            });
        }

        session.IPAddress = null;
        session.AgentInfo = null;
        session.SavedSteps?.Clear();

        context.Subject.ScrubJson(session.Properties, context.ReplacementText);
    }

    private async Task FindAsync(CommercePersonalDataSubject subject, string ownerId, string email, CancellationToken cancellationToken)
    {
        for (var skip = 0; skip < MaxSessions; skip += PageSize)
        {
            var sessions = await _sessionStore.GetByCustomerAsync(ownerId, email, skip, PageSize, cancellationToken);

            foreach (var session in sessions)
            {
                subject.AddCheckoutSession(session.SessionId);

                if (string.IsNullOrEmpty(session.OwnerId))
                {
                    subject.AddOwner(CustomerOwner.ForGuest(session.SessionId));
                }

                // The name is only scrubbed from the person's own records. The email is not followed: a signed-in user
                // can enter someone else's contact when buying for them, and following it would reach that person's
                // guest checkouts.
                if (session.TryGet<CheckoutContactInfo>(out var contact) && contact is not null)
                {
                    subject.AddName(contact.DisplayName);
                }
            }

            if (sessions.Count < PageSize)
            {
                break;
            }
        }
    }

    private async Task<IReadOnlyList<CheckoutSession>> LoadAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var sessions = new List<CheckoutSession>();

        foreach (var batch in subject.CheckoutSessionIds.Order(StringComparer.Ordinal).Chunk(BatchSize))
        {
            sessions.AddRange(await _sessionStore.GetManyAsync(batch, cancellationToken));
        }

        return [.. sessions.OrderBy(session => session.CreatedUtc)];
    }
}
