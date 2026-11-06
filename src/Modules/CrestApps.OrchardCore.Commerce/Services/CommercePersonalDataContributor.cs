using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// Bridges the commerce personal-data contract onto <see cref="IPersonalDataService"/>: it runs every
/// <see cref="ICommercePersonalDataContributor"/> through <see cref="ICommercePersonalDataService"/> and turns the
/// commerce export into open records, so a privacy request reaches checkouts, transactions, payments, refunds,
/// subscriptions, installment plans, coupons, provider customers, stock alerts and the verticals' records.
/// </summary>
/// <remarks>
/// It runs first, because the commerce contributors follow checkout emails and guest owners to the person's records,
/// which other contributors may erase.
/// </remarks>
public sealed class CommercePersonalDataContributor : IPersonalDataContributor
{
    /// <summary>The record kind of a commerce transaction.</summary>
    public const string TransactionKind = "Transaction";

    /// <summary>The record kind of a payment.</summary>
    public const string PaymentKind = "Payment";

    /// <summary>The record kind of a refund.</summary>
    public const string RefundKind = "Refund";

    /// <summary>The record kind of an installment plan.</summary>
    public const string InstallmentPlanKind = "InstallmentPlan";

    /// <summary>The record kind of a subscription.</summary>
    public const string SubscriptionKind = "Subscription";

    /// <summary>The record kind of a checkout session.</summary>
    public const string CheckoutSessionKind = "CheckoutSession";

    /// <summary>The record kind of a coupon redemption.</summary>
    public const string CouponRedemptionKind = "CouponRedemption";

    /// <summary>The record kind of a customer kept by a payment provider.</summary>
    public const string ProviderCustomerKind = "ProviderCustomer";

    /// <summary>The record kind of a back-in-stock alert.</summary>
    public const string StockAlertKind = "StockAlert";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private readonly ICommercePersonalDataService _commercePersonalDataService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommercePersonalDataContributor"/> class.
    /// </summary>
    /// <param name="commercePersonalDataService">The commerce personal data service.</param>
    public CommercePersonalDataContributor(ICommercePersonalDataService commercePersonalDataService)
    {
        _commercePersonalDataService = commercePersonalDataService;
    }

    /// <inheritdoc/>
    public int Order => -100;

    /// <inheritdoc/>
    /// <remarks>
    /// The guest retention expiry has always erased the commerce records, so commerce takes part in it.
    /// </remarks>
    public bool TakesPartInRetentionExpiry => true;

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!TryGetOwner(context.Subject, out var owner, out var email))
        {
            return;
        }

        var export = await _commercePersonalDataService.ExportAsync(owner, email, cancellationToken);

        Add(context, TransactionKind, export.Transactions);
        Add(context, PaymentKind, export.Payments);
        Add(context, RefundKind, export.Refunds);
        Add(context, InstallmentPlanKind, export.InstallmentPlans);
        Add(context, SubscriptionKind, export.Subscriptions);
        Add(context, CheckoutSessionKind, export.CheckoutSessions);
        Add(context, CouponRedemptionKind, export.CouponRedemptions);
        Add(context, ProviderCustomerKind, export.ProviderCustomers);
        Add(context, StockAlertKind, export.StockAlerts);

        foreach (var record in export.Records)
        {
            var added = context.Add(record.Kind, record.Id, record.Kind, record.CreatedUtc);

            foreach (var value in record.Values)
            {
                added.Fields[value.Key] = value.Value;
            }
        }

        foreach (var warning in export.Warnings)
        {
            context.Export.Warnings.Add(warning);
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!TryGetOwner(context.Subject, out var owner, out var email))
        {
            return;
        }

        var result = context.Kind == PersonalDataRequestKind.RetentionExpiry
            ? await _commercePersonalDataService.ExpireAsync(owner, email, cancellationToken)
            : await _commercePersonalDataService.AnonymizeAsync(owner, email, context.ReplacementText, cancellationToken);

        context.Result.Count(TransactionKind, result.Transactions);
        context.Result.Count(PaymentKind, result.Payments);
        context.Result.Count(RefundKind, result.Refunds);
        context.Result.Count(InstallmentPlanKind, result.InstallmentPlans);
        context.Result.Count(SubscriptionKind, result.Subscriptions);
        context.Result.Count(CheckoutSessionKind, result.CheckoutSessions);
        context.Result.Count(CouponRedemptionKind, result.CouponRedemptions);
        context.Result.Count(ProviderCustomerKind, result.ProviderCustomers);
        context.Result.Count(StockAlertKind, result.StockAlerts);

        foreach (var record in result.Records)
        {
            context.Result.Count(record.Key, record.Value);
        }

        foreach (var warning in result.Warnings)
        {
            context.Result.Warnings.Add(warning);
        }
    }

    private static bool TryGetOwner(PersonalDataSubject subject, out CustomerOwner owner, out string email)
    {
        owner = string.IsNullOrEmpty(subject.UserId) ? null : CustomerOwner.ForUser(subject.UserId);
        email = subject.Email;

        // Commerce finds a person by their account or the email they checked out with.
        return owner is not null || !string.IsNullOrWhiteSpace(email);
    }

    private static void Add<T>(PersonalDataExportContext context, string kind, IEnumerable<T> records)
    {
        foreach (var record in records)
        {
            if (JsonSerializer.SerializeToNode(record, _options) is not JsonObject json)
            {
                continue;
            }

            var id = Text(json["itemId"]) ?? Text(json["checkoutSessionId"]) ?? Text(json["customerReference"]) ?? Text(json["contentItemId"]);
            var title = Text(json["title"]) ?? Text(json["code"]) ?? Text(json["providerKey"]) ?? kind;
            var createdUtc = json["createdUtc"] is JsonValue created && created.TryGetValue<DateTime>(out var utc) ? utc : (DateTime?)null;

            var added = context.Add(kind, id, title, createdUtc);

            foreach (var property in json)
            {
                if (property.Value is null)
                {
                    continue;
                }

                added.Fields[property.Key] = Text(property.Value) ?? property.Value.ToJsonString(_options);
            }
        }
    }

    private static string Text(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        return value.TryGetValue<DateTime>(out var date)
            ? date.ToString("O", CultureInfo.InvariantCulture)
            : value.ToJsonString(_options);
    }
}
