using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Reports;

/// <summary>
/// Provides the shared category, permission, and formatting used by the subscription reports contributed
/// to the admin Reports area.
/// </summary>
public abstract class SubscriptionReportBase : IReport
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionReportBase"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the subscription currency.</param>
    /// <param name="stringLocalizer">The string localizer used for the report labels.</param>
    protected SubscriptionReportBase(ISiteService siteService, IStringLocalizer stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer used for the report labels.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract LocalizedString DisplayName { get; }

    /// <inheritdoc/>
    public abstract LocalizedString Description { get; }

    /// <inheritdoc/>
    public virtual string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public Permission Permission => SubscriptionPermissions.ManageSubscriptions;

    /// <inheritdoc/>
    public abstract Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the configured subscription currency code, falling back to <c>USD</c>.
    /// </summary>
    /// <returns>The currency code.</returns>
    protected async Task<string> GetCurrencyAsync()
    {
        var settings = await _siteService.GetSettingsAsync<SubscriptionSettings>();

        return string.IsNullOrWhiteSpace(settings?.Currency)
            ? "USD"
            : settings.Currency;
    }

    /// <summary>
    /// Formats a monetary amount using the invariant culture, prefixed with the currency code.
    /// </summary>
    /// <param name="value">The amount to format.</param>
    /// <param name="currency">The currency code.</param>
    /// <returns>The formatted amount (for example <c>USD 1,234.50</c>).</returns>
    protected static string FormatCurrency(double value, string currency)
    {
        var amount = value.ToString("N2", CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(currency)
            ? amount
            : currency + " " + amount;
    }
}
