using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// Says when a Pay Later balance is due, the way invoices and B2B checkouts set a due date: the payer's own payment term
/// (from their billing profile, such as an account on Net 60) wins, then the payment term chosen for Pay Later, and with
/// neither the number of days in the Pay Later settings. A payment term gives a date, so the balance is due at the end of
/// that day in the site's time zone, as an invoice is.
/// </summary>
/// <remarks>
/// The terms come from the Payment Terms feature and the billing profiles from Parties; both are optional, so a site
/// without them keeps the number of days.
/// </remarks>
public sealed class PayLaterDueDateResolver
{
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayLaterDueDateResolver"/> class.
    /// </summary>
    /// <param name="siteService">The site service the Pay Later settings are read from.</param>
    /// <param name="serviceProvider">The service provider the optional payment term, billing profile and clock services are resolved from.</param>
    public PayLaterDueDateResolver(ISiteService siteService, IServiceProvider serviceProvider)
    {
        _siteService = siteService;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Returns when a balance that starts at <paramref name="startUtc"/> is due.
    /// </summary>
    /// <param name="startUtc">When the balance starts: the checkout, or the start of the cycle it bills.</param>
    /// <param name="payer">The party that owes it, or an empty reference.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due time in UTC, or <see langword="null"/> when the balance has no due date.</returns>
    public async Task<DateTime?> GetDueUtcAsync(DateTime startUtc, PartyRef payer, CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<PayLaterSettings>();

        if (await FindTermAsync(settings, payer, cancellationToken) is { } term &&
            _serviceProvider.GetService<IPaymentTermCalculator>() is { } calculator)
        {
            var localClock = _serviceProvider.GetService<ILocalClock>();
            var utc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
            var start = localClock is null
                ? DateOnly.FromDateTime(utc)
                : DateOnly.FromDateTime((await localClock.ConvertToLocalAsync(new DateTimeOffset(utc))).DateTime);
            var endOfDueDay = calculator.GetDueDate(term, start).ToDateTime(new TimeOnly(23, 59, 59));

            return localClock is null
                ? DateTime.SpecifyKind(endOfDueDay, DateTimeKind.Utc)
                : await localClock.ConvertToUtcAsync(endOfDueDay);
        }

        return settings.NetTermDays > 0 ? startUtc.AddDays(settings.NetTermDays) : null;
    }

    /// <summary>
    /// Returns the payment term a balance of <paramref name="payer"/> follows, or <see langword="null"/> when it follows
    /// the number of days.
    /// </summary>
    /// <param name="payer">The party that owes it, or an empty reference.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The term, or <see langword="null"/>.</returns>
    public async Task<PaymentTerm> FindTermAsync(PartyRef payer, CancellationToken cancellationToken = default)
        => await FindTermAsync(await _siteService.GetSettingsAsync<PayLaterSettings>(), payer, cancellationToken);

    private async Task<PaymentTerm> FindTermAsync(PayLaterSettings settings, PartyRef payer, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<IPaymentTermManager>() is not { } terms)
        {
            return null;
        }

        // A guest has no billing profile; anyone else may have agreed terms of their own.
        if (!payer.IsEmpty &&
            !string.Equals(payer.Kind, PartyKinds.Guest, StringComparison.OrdinalIgnoreCase) &&
            _serviceProvider.GetService<IPartyBillingProfileService>() is { } profiles &&
            await profiles.GetAsync(payer, cancellationToken) is { PaymentTermId.Length: > 0 } profile &&
            await terms.FindByIdAsync(profile.PaymentTermId, cancellationToken) is { Enabled: true } own)
        {
            return own;
        }

        if (!string.IsNullOrEmpty(settings.PaymentTermId) &&
            await terms.FindByIdAsync(settings.PaymentTermId, cancellationToken) is { Enabled: true } chosen)
        {
            return chosen;
        }

        return null;
    }
}
