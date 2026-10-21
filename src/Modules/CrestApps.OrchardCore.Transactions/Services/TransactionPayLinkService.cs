using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPayLinkService"/> class.
    /// </summary>
    /// <param name="signedTokenService">The service that signs the token.</param>
    /// <param name="siteUrlGenerator">The generator of the link's absolute URL.</param>
    public TransactionPayLinkService(
        SignedTokenService signedTokenService,
        SiteUrlGenerator siteUrlGenerator)
    {
        _signedTokenService = signedTokenService;
        _siteUrlGenerator = siteUrlGenerator;
    }

    /// <inheritdoc/>
    public Task<string> CreatePayUrlAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        // A reminder is often sent by a background task, or from an administrator's request on an internal address,
        // and the link has to work for the customer, so the site's configured address wins.
        return _siteUrlGenerator.GetUrlAsync(TransactionsConstants.RouteNames.Pay, new { token = _signedTokenService.Protect(ProtectionPurpose, transaction.ItemId, Lifetime) });
    }

    /// <inheritdoc/>
    public string GetTransactionId(string token)
        => _signedTokenService.Unprotect(ProtectionPurpose, token);
}
