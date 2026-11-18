using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;

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

    private readonly SignedTokenService _signedTokenService;
    private readonly SiteUrlGenerator _siteUrlGenerator;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPayLinkService"/> class.
    /// </summary>
    /// <param name="signedTokenService">The service that signs the token.</param>
    /// <param name="siteUrlGenerator">The generator of the link's absolute URL.</param>
    /// <param name="serviceProvider">The provider the <see cref="ITransactionPayUrlProvider"/> services are resolved from, or <see langword="null"/>.</param>
    public TransactionPayLinkService(
        SignedTokenService signedTokenService,
        SiteUrlGenerator siteUrlGenerator,
        IServiceProvider serviceProvider = null)
    {
        _signedTokenService = signedTokenService;
        _siteUrlGenerator = siteUrlGenerator;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<string> CreatePayUrlAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        // A transaction paid through a page of its own, such as the checkout of the order an invoice bills, is linked there.
        if (await TransactionPayUrls.GetOwnPayUrlAsync(_serviceProvider, transaction, cancellationToken) is { } own)
        {
            return own;
        }

        // A reminder is often sent by a background task, or from an administrator's request on an internal address,
        // and the link has to work for the customer, so the site's configured address wins.
        return await _siteUrlGenerator.GetUrlAsync(TransactionsConstants.RouteNames.Pay, new { token = _signedTokenService.Protect(ProtectionPurpose, transaction.ItemId, Lifetime) });
    }

    /// <inheritdoc/>
    public string GetTransactionId(string token)
        => _signedTokenService.Unprotect(ProtectionPurpose, token);
}

/// <summary>
/// Finds the page of its own a transaction is paid through (<see cref="ITransactionPayUrlProvider"/>).
/// </summary>
internal static class TransactionPayUrls
{
    public static async Task<string> GetOwnPayUrlAsync(IServiceProvider serviceProvider, Transaction transaction, CancellationToken cancellationToken)
    {
        foreach (var provider in serviceProvider?.GetServices<ITransactionPayUrlProvider>() ?? [])
        {
            if (await provider.GetPayUrlAsync(transaction, cancellationToken) is { Length: > 0 } url)
            {
                return url;
            }
        }

        return null;
    }
}
