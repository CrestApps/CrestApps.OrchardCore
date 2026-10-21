using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.PayLater.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// Pay Later: an offline commitment to pay an invoice by its due date, for one-time purchases and subscriptions.
/// </summary>
/// <remarks>
/// Like the other offline methods it never touches a gateway; what makes a subscription recur is the schedule the
/// checkout handler writes on the resulting transaction, which the renewal sweep follows.
/// </remarks>
public sealed class PayLaterCheckoutPaymentProvider : OfflineCheckoutPaymentProvider, ICheckoutRecurringPaymentProvider
{
    /// <summary>
    /// The stable processor key that identifies the Pay Later provider.
    /// </summary>
    public const string ProcessorKey = "pay-later";

    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public PayLaterCheckoutPaymentProvider(
        ISiteService siteService,
        IHostEnvironment hostEnvironment,
        IStringLocalizer<PayLaterCheckoutPaymentProvider> stringLocalizer)
        : base(hostEnvironment)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Key => ProcessorKey;

    /// <inheritdoc/>
    public override string DisplayName => S["Pay Later"];

    /// <inheritdoc/>
    public override string Description => S["Confirm now and pay later."];

    /// <inheritdoc/>
    public override PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsRecurringPayments = true,
        SupportsHostedCheckout = false,
        SupportsEmbeddedElements = false,
        SupportsCombinedOneTimeAndRecurring = true,
        CollectsTaxDynamically = false,
        SupportsRefunds = false,

        // A commitment to pay later collects nothing, so it cannot pay off a balance that already exists.
        DefersPayment = true,
    };

    /// <inheritdoc/>
    public override async Task<DateTime?> GetDueUtcAsync(DateTime now)
    {
        var settings = await _siteService.GetSettingsAsync<PayLaterSettings>();

        return settings.NetTermDays > 0 ? now.AddDays(settings.NetTermDays) : null;
    }

    /// <inheritdoc/>
    public override async Task<bool> IsAvailableAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
        => (await _siteService.GetSettingsAsync<PayLaterSettings>()).Enabled;

    /// <inheritdoc/>
    /// <remarks>
    /// There is no gateway to establish an agreement with, so the commitment is recorded locally exactly like a
    /// one-time one.
    /// </remarks>
    public Task<PaymentBeginResult> BeginRecurringAsync(BeginRecurringPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var reference = string.IsNullOrEmpty(context.Attempt?.ProviderReference)
            ? IdGenerator.GenerateId()
            : context.Attempt.ProviderReference;

        return Task.FromResult(PaymentBeginResult.Success(reference));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Stopping an offline agreement is a local decision, so this always succeeds. The caller is what marks the
    /// ledger's recurrence canceled, which is what actually stops the next cycle from being created.
    /// </remarks>
    public Task<RecurringCancelResult> CancelRecurringAsync(CancelRecurringPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(RecurringCancelResult.Success());

    /// <inheritdoc/>
    public Task<RecurringPauseResult> PauseRecurringAsync(PauseRecurringPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(RecurringPauseResult.Success());

    /// <inheritdoc/>
    /// <remarks>
    /// The amount billed each cycle is read from the ledger when the next cycle is created, so there is nothing to
    /// change at a gateway.
    /// </remarks>
    public Task<RecurringUpdateResult> UpdateRecurringAsync(UpdateRecurringPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(RecurringUpdateResult.Success());
}
