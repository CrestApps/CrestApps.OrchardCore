using System.Security.Cryptography;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The default <see cref="ITransactionPayLinkService"/>. The token is the transaction identifier protected with the
/// tenant's data protection keys and an expiry, so it cannot be forged, altered to name another transaction, or used
/// after it expires.
/// </summary>
public sealed class TransactionPayLinkService : ITransactionPayLinkService
{
    /// <summary>
    /// How long a pay link keeps working after it is created.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    private const string ProtectionPurpose = "CrestApps.OrchardCore.Transactions.PayLink";

    private readonly ITimeLimitedDataProtector _protector;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPayLinkService"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <param name="siteService">The site service, for the site's public base URL.</param>
    /// <param name="httpContextAccessor">The accessor for the current request, used when no base URL is configured.</param>
    /// <param name="linkGenerator">The link generator.</param>
    public TransactionPayLinkService(
        IDataProtectionProvider dataProtectionProvider,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose).ToTimeLimitedDataProtector();
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    /// <inheritdoc/>
    public async Task<string> CreatePayUrlAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var token = _protector.Protect(transaction.ItemId, Lifetime);

        // The site's configured address wins: a reminder is often sent by a background task, or from an
        // administrator's request on an internal address, and the link has to work for the customer.
        var site = await _siteService.GetSiteSettingsAsync();

        if (!string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return $"{site.BaseUrl.TrimEnd('/')}/transactions/pay/{Uri.EscapeDataString(token)}";
        }

        var httpContext = _httpContextAccessor.HttpContext;

        return httpContext is null
            ? null
            : _linkGenerator.GetUriByRouteValues(httpContext, TransactionsConstants.RouteNames.Pay, new { token });
    }

    /// <inheritdoc/>
    public string GetTransactionId(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(token, out _);
        }
        catch (CryptographicException)
        {
            // Altered, expired, or protected by another site's keys.
            return null;
        }
    }
}
