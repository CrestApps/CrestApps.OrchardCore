using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The default <see cref="ITransactionCustomerLinkService"/>. It links the customer pages in the site theme or in the
/// admin, as <see cref="TransactionCustomerPagesOptions.LinkTarget"/> says, at the site's public address.
/// </summary>
public sealed class TransactionCustomerLinkService : ITransactionCustomerLinkService
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly TransactionCustomerPagesOptions _options;
    private readonly AdminOptions _adminOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCustomerLinkService"/> class.
    /// </summary>
    /// <param name="siteService">The site service, for the site's public base URL.</param>
    /// <param name="httpContextAccessor">The accessor for the current request, used when no base URL is configured.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="options">The customer pages options, which say which pages the links open.</param>
    /// <param name="adminOptions">The admin options, for the admin's address prefix.</param>
    public TransactionCustomerLinkService(
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IOptions<TransactionCustomerPagesOptions> options,
        IOptions<AdminOptions> adminOptions)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _options = options.Value;
        _adminOptions = adminOptions.Value;
    }

    /// <inheritdoc/>
    public Task<string> GetStatementUrlAsync(CancellationToken cancellationToken = default)
        => CreateUrlAsync(
            TransactionsConstants.RouteNames.Statement,
            "my-transactions",
            TransactionsConstants.RouteNames.AdminStatement,
            "my-transactions",
            new RouteValueDictionary());

    /// <inheritdoc/>
    public Task<string> GetInvoiceUrlAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var itemId = Uri.EscapeDataString(transaction.ItemId);

        return CreateUrlAsync(
            TransactionsConstants.RouteNames.Invoice,
            $"my-transactions/invoices/{itemId}",
            TransactionsConstants.RouteNames.AdminInvoice,
            $"transaction-invoices/{itemId}",
            new RouteValueDictionary
            {
                ["itemId"] = transaction.ItemId,
            });
    }

    /// <inheritdoc/>
    public async Task<string> GetReceiptUrlAsync(Transaction transaction, TransactionEvent payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(payment);

        // A payment recorded before events had identifiers cannot be addressed, so it has no receipt page.
        if (string.IsNullOrEmpty(payment.Id))
        {
            return null;
        }

        var itemId = Uri.EscapeDataString(transaction.ItemId);
        var paymentId = Uri.EscapeDataString(payment.Id);

        return await CreateUrlAsync(
            TransactionsConstants.RouteNames.Receipt,
            $"my-transactions/receipts/{itemId}/{paymentId}",
            TransactionsConstants.RouteNames.AdminReceipt,
            $"transaction-receipts/{itemId}/{paymentId}",
            new RouteValueDictionary
            {
                ["itemId"] = transaction.ItemId,
                ["paymentId"] = payment.Id,
            });
    }

    private async Task<string> CreateUrlAsync(
        string siteRouteName,
        string sitePath,
        string adminRouteName,
        string adminPath,
        RouteValueDictionary values)
    {
        var admin = _options.LinkTarget == TransactionCustomerLinkTarget.Admin;

        // The site's configured address wins: these links are sent in messages, often by a background task or from
        // an administrator's request on an internal address, and they have to work for the customer.
        var site = await _siteService.GetSiteSettingsAsync();

        if (!string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            var path = admin
                ? $"{_adminOptions.AdminUrlPrefix.Trim('/')}/{adminPath}"
                : sitePath;

            return $"{site.BaseUrl.TrimEnd('/')}/{path}";
        }

        var httpContext = _httpContextAccessor.HttpContext;

        return httpContext is null
            ? null
            : _linkGenerator.GetUriByRouteValues(httpContext, admin ? adminRouteName : siteRouteName, values);
    }
}
