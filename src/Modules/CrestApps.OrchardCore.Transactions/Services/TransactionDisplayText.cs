using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The words the transaction screens show for stored values: payment states, refund statuses, timeline
/// events, settlement methods, sources, and payment methods. The stored values are identifiers, not text a
/// customer or an operator should have to read.
/// </summary>
public sealed class TransactionDisplayText
{
    private readonly IEnumerable<ICheckoutPaymentProvider> _paymentProviders;
    private readonly TransactionSourceOptions _sourceOptions;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionDisplayText"/> class.
    /// </summary>
    /// <param name="paymentProviders">The registered payment providers, which name their own methods.</param>
    /// <param name="sourceOptions">The registered transaction sources, which name themselves.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="localizerFactory">The localizer factory that translates the registered transaction sources' names.</param>
    public TransactionDisplayText(
        IEnumerable<ICheckoutPaymentProvider> paymentProviders,
        IOptions<TransactionSourceOptions> sourceOptions,
        IStringLocalizer<TransactionDisplayText> stringLocalizer,
        IStringLocalizerFactory localizerFactory)
    {
        _paymentProviders = paymentProviders;
        _sourceOptions = sourceOptions.Value;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the label for a payment attempt's state.
    /// </summary>
    /// <param name="state">The state.</param>
    public string PaymentStateName(PaymentAttemptState state)
        => state switch
        {
            PaymentAttemptState.Created => S["Started"],
            PaymentAttemptState.Pending => S["Processing"],
            PaymentAttemptState.Succeeded => S["Succeeded"],
            PaymentAttemptState.Failed => S["Failed"],
            PaymentAttemptState.Canceled => S["Canceled"],
            _ => state.ToString(),
        };

    /// <summary>
    /// Gets the label for a refund's status.
    /// </summary>
    /// <param name="status">The status.</param>
    public string RefundStatusName(RefundStatus status)
        => status switch
        {
            RefundStatus.Requested => S["Requested"],
            RefundStatus.Pending => S["Processing"],
            RefundStatus.Succeeded => S["Refunded"],
            RefundStatus.Failed => S["Failed"],
            RefundStatus.Canceled => S["Canceled"],
            RefundStatus.PendingManualReview => S["Needs manual settlement"],
            _ => status.ToString(),
        };

    /// <summary>
    /// Gets the heading for a timeline event.
    /// </summary>
    /// <param name="type">The event type.</param>
    public string EventTypeName(TransactionEventType type)
        => type switch
        {
            TransactionEventType.Created => S["Created"],
            TransactionEventType.StatusChanged => S["Status changed"],
            TransactionEventType.PaymentRecorded => S["Payment received"],
            TransactionEventType.ReminderSent => S["Reminder sent"],
            TransactionEventType.Canceled => S["Canceled"],
            TransactionEventType.Note => S["Note"],
            TransactionEventType.Refunded => S["Refunded"],
            TransactionEventType.Adjusted => S["Adjusted"],
            TransactionEventType.InvoiceSent => S["Invoice sent"],
            _ => type.ToString(),
        };

    /// <summary>
    /// Gets the label for how a transaction was settled.
    /// </summary>
    /// <param name="method">The stored settlement method.</param>
    public string SettlementMethodName(string method)
    {
        if (string.Equals(method, TransactionsConstants.SettlementMethods.Online, StringComparison.OrdinalIgnoreCase))
        {
            return S["Paid online"];
        }

        if (string.Equals(method, TransactionsConstants.SettlementMethods.Offline, StringComparison.OrdinalIgnoreCase))
        {
            return S["Recorded by hand"];
        }

        if (string.Equals(method, TransactionsConstants.SettlementMethods.Adjusted, StringComparison.OrdinalIgnoreCase))
        {
            return S["Nothing left to pay after an adjustment"];
        }

        return method;
    }

    /// <summary>
    /// Gets the label for why the amount owed was lowered.
    /// </summary>
    /// <param name="kind">The adjustment kind.</param>
    public string AdjustmentKindName(TransactionAdjustmentKind kind)
        => kind switch
        {
            TransactionAdjustmentKind.Discount => S["Discount"],
            TransactionAdjustmentKind.Waiver => S["Waiver"],
            TransactionAdjustmentKind.Credit => S["Credit"],
            TransactionAdjustmentKind.WriteOff => S["Write-off"],
            _ => kind.ToString(),
        };

    /// <summary>
    /// Gets the name of the feature that created a transaction, falling back to its stored identifier when that
    /// feature is no longer enabled.
    /// </summary>
    /// <param name="source">The stored source.</param>
    public string SourceName(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        return _sourceOptions.Sources.TryGetValue(source, out var registered) && registered.DisplayName is not null
            ? _localizerFactory.Localize(registered.DisplayName).Value
            : source;
    }

    /// <summary>
    /// Gets the name of a payment method, falling back to its stored key when its provider is no longer enabled.
    /// </summary>
    /// <param name="providerKey">The stored provider key.</param>
    public string PaymentMethodName(string providerKey)
    {
        if (string.IsNullOrEmpty(providerKey))
        {
            return null;
        }

        var provider = _paymentProviders.FirstOrDefault(provider => string.Equals(provider.Key, providerKey, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrEmpty(provider?.DisplayName) ? providerKey : provider.DisplayName;
    }

    /// <summary>
    /// Gets the provider of a payment method, or <see langword="null"/> when it is no longer enabled.
    /// </summary>
    /// <param name="providerKey">The stored provider key.</param>
    public ICheckoutPaymentProvider FindPaymentProvider(string providerKey)
        => string.IsNullOrEmpty(providerKey)
            ? null
            : _paymentProviders.FirstOrDefault(provider => string.Equals(provider.Key, providerKey, StringComparison.OrdinalIgnoreCase));
}
