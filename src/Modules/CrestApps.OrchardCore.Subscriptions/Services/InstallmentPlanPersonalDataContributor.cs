using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Exports and anonymizes the installment plans of a person, found by their account and by the email recorded on the
/// plan, and adds the checkouts and payment-provider customer of each plan to what identifies them.
/// </summary>
/// <remarks>
/// Anonymizing removes the customer's name and email, the internal note, the description of the saved card and the
/// person's emails and names from the title and history. The schedule, amounts, statuses and dates stay, and so do the
/// provider's references to the saved card, so a plan that is still open can still be collected.
/// </remarks>
public sealed class InstallmentPlanPersonalDataContributor : ICommercePersonalDataContributor
{
    internal const int PageSize = 100;
    internal const int MaxPlans = 10_000;

    private readonly IInstallmentPlanStore _planStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlanPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="planStore">The installment plan store.</param>
    public InstallmentPlanPersonalDataContributor(IInstallmentPlanStore planStore)
    {
        _planStore = planStore;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var plan in await GetPlansAsync(subject, cancellationToken))
        {
            subject.AddName(plan.CustomerName);
            subject.AddCheckoutSession(plan.DownPaymentSessionId);

            foreach (var payment in plan.Payments ?? [])
            {
                foreach (var sessionId in payment.CheckoutSessionIds ?? [])
                {
                    subject.AddCheckoutSession(sessionId);
                }
            }

            if (plan.PaymentMethod is { } paymentMethod)
            {
                subject.AddProviderCustomer(paymentMethod.ProviderKey, paymentMethod.CustomerReference);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var plan in await GetPlansAsync(context.Subject, cancellationToken))
        {
            var record = new CommerceInstallmentPlanRecord
            {
                ItemId = plan.ItemId,
                Title = plan.Title,
                OwnerId = plan.OwnerId,
                CustomerName = plan.CustomerName,
                CustomerEmail = plan.CustomerEmail,
                Status = plan.Status.ToString(),
                Currency = plan.Currency,
                TotalAmount = plan.TotalAmount,
                DownPaymentAmount = plan.DownPaymentAmount,
                AmountPaid = plan.AmountPaid,
                InstallmentCount = plan.InstallmentCount,
                Frequency = plan.Frequency.ToString(),
                CollectionMethod = plan.CollectionMethod.ToString(),
                PaymentMethod = plan.PaymentMethod?.Describe(),
                Notes = plan.Notes,
                CreatedUtc = plan.CreatedUtc,
                CompletedUtc = plan.CompletedUtc,
                CanceledUtc = plan.CanceledUtc,
            };

            foreach (var payment in (plan.Payments ?? []).OrderBy(payment => payment.Number))
            {
                if (!string.IsNullOrEmpty(payment.TransactionId))
                {
                    record.TransactionIds.Add(payment.TransactionId);
                }
            }

            context.Export.InstallmentPlans.Add(record);
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var plan in await GetPlansAsync(context.Subject, cancellationToken))
        {
            Anonymize(plan, context);

            await _planStore.UpdateAsync(plan, cancellationToken);

            context.Result.InstallmentPlans++;
        }
    }

    /// <summary>
    /// Removes the personal data from one plan, leaving its schedule, amounts, statuses and dates as they were.
    /// </summary>
    internal static void Anonymize(InstallmentPlan plan, CommercePersonalDataAnonymizeContext context)
    {
        plan.CustomerName = context.ReplaceName(plan.CustomerName);
        plan.CustomerEmail = null;
        plan.Title = context.Scrub(plan.Title);

        // A note is free text an administrator wrote about the customer, so none of it is kept.
        plan.Notes = null;

        if (plan.PaymentMethod is { } paymentMethod)
        {
            paymentMethod.Brand = null;
            paymentMethod.Last4 = null;
            paymentMethod.ExpirationMonth = null;
            paymentMethod.ExpirationYear = null;
        }

        foreach (var payment in plan.Payments ?? [])
        {
            payment.LastFailureMessage = context.Scrub(payment.LastFailureMessage);
        }

        foreach (var item in plan.Events ?? [])
        {
            item.Message = context.Scrub(item.Message);
            item.ActorName = context.Scrub(item.ActorName);
        }
    }

    private async Task<IReadOnlyList<InstallmentPlan>> GetPlansAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var plans = new Dictionary<string, InstallmentPlan>(StringComparer.Ordinal);

        foreach (var userId in subject.UserIds.ToArray())
        {
            await CollectAsync(plans, new InstallmentPlanQuery { OwnerId = userId }, _ => true, cancellationToken);
        }

        // The search also matches titles and names that merely contain the email, so only an exact match is kept.
        foreach (var email in subject.Emails.ToArray())
        {
            await CollectAsync(plans, new InstallmentPlanQuery { Search = email }, plan => subject.IsEmail(plan.CustomerEmail), cancellationToken);
        }

        return [.. plans.Values.OrderBy(plan => plan.CreatedUtc)];
    }

    private async Task CollectAsync(Dictionary<string, InstallmentPlan> plans, InstallmentPlanQuery query, Func<InstallmentPlan, bool> belongs, CancellationToken cancellationToken)
    {
        for (var page = 1; (page - 1) * PageSize < MaxPlans; page++)
        {
            var result = await _planStore.PageAsync(page, PageSize, query, cancellationToken);
            var entries = result.Entries?.ToArray() ?? [];

            foreach (var plan in entries)
            {
                if (belongs(plan))
                {
                    plans.TryAdd(plan.ItemId, plan);
                }
            }

            if (entries.Length < PageSize)
            {
                break;
            }
        }
    }
}
