using System.Globalization;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Offers the installments an administrator configured on the installment plan settings, for the transaction sources
/// they chose. It lets a site offer "3 or 6 monthly payments" for, say, every Pay Later balance without writing code.
/// A module that wants its own rules implements <see cref="IInstallmentOptionProvider"/> for its source instead.
/// </summary>
public sealed class SiteSettingsInstallmentOptionProvider : IInstallmentOptionProvider
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteSettingsInstallmentOptionProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the settings.</param>
    public SiteSettingsInstallmentOptionProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The settings name the sources, so the provider is asked about every transaction and filters by itself.
    /// </remarks>
    public string Source
        => null;

    /// <inheritdoc/>
    public async Task<IEnumerable<InstallmentOption>> GetOptionsAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var settings = await _siteService.GetSettingsAsync<InstallmentPlanSettings>();

        if (settings.CheckoutSources is not { Length: > 0 } ||
            settings.CheckoutInstallmentCounts is not { Length: > 0 } ||
            !settings.CheckoutSources.Contains(transaction.Source, StringComparer.OrdinalIgnoreCase) ||
            transaction.OutstandingAmount < settings.CheckoutMinimumAmount)
        {
            return [];
        }

        return settings.CheckoutInstallmentCounts
            .Where(count => count > 0)
            .Distinct()
            .Order()
            .Select(count => new InstallmentOption
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"{count}-{settings.CheckoutFrequency}"),
                InstallmentCount = count,
                Frequency = settings.CheckoutFrequency,
            })
            .ToArray();
    }
}
