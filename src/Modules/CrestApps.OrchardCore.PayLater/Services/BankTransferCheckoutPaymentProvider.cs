using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.PayLater.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// Bank transfer: the order is placed now, the buyer transfers the money using the store's bank details and the
/// order number as the reference, and the store records the payment when it arrives.
/// </summary>
public sealed class BankTransferCheckoutPaymentProvider : OfflineCheckoutPaymentProvider
{
    /// <summary>
    /// The stable processor key of the Bank transfer method.
    /// </summary>
    public const string ProcessorKey = "bank-transfer";

    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public BankTransferCheckoutPaymentProvider(
        ISiteService siteService,
        IHostEnvironment hostEnvironment,
        IStringLocalizer<BankTransferCheckoutPaymentProvider> stringLocalizer)
        : base(hostEnvironment)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Key => ProcessorKey;

    /// <inheritdoc/>
    public override string DisplayName => S["Bank transfer"];

    /// <inheritdoc/>
    public override string Description => S["Place the order now and transfer the money from your bank."];

    /// <inheritdoc/>
    public override async Task<DateTime?> GetDueUtcAsync(DateTime now)
    {
        var settings = await _siteService.GetSettingsAsync<BankTransferSettings>();

        return settings.NetTermDays > 0 ? now.AddDays(settings.NetTermDays) : null;
    }

    /// <inheritdoc/>
    public override async Task<bool> IsAvailableAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<BankTransferSettings>();

        // Without bank details the buyer would commit to a transfer they can't make.
        return settings.Enabled && !string.IsNullOrWhiteSpace(settings.Instructions);
    }
}
