using System.Security.Claims;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Makes transactions, such as invoices, a record kind (<see cref="TransactionsConstants.RecordKind"/>), so any module
/// can link to a transaction, pick one and show its title without referencing Transactions. The id is the
/// transaction's item id. Users who manage transactions may search them, by title, as the transactions report does.
/// </summary>
public sealed class TransactionRecordSource : IRecordSource
{
    private const string DetailAction = "Detail";
    private const string DetailController = "Admin";

    private readonly ITransactionStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionRecordSource"/> class.
    /// </summary>
    /// <param name="store">The transaction store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionRecordSource(
        ITransactionStore store,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<TransactionRecordSource> stringLocalizer)
    {
        _store = store;
        _authorizationService = authorizationService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => TransactionsConstants.RecordKind;

    /// <inheritdoc/>
    public LocalizationSource DisplayName => LocalizationSource.Create<TransactionRecordSource>("Transaction");

    /// <inheritdoc/>
    public async Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user is not null && await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ManageTransactions);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        if (search.Count <= 0 || !await CanSearchAsync(search.User, cancellationToken))
        {
            return [];
        }

        var text = search.Text?.Trim();
        var page = await _store.PageAsync(1, search.Count, new TransactionQuery { Search = string.IsNullOrEmpty(text) ? null : text }, cancellationToken);

        return [.. page.Entries.Select(ToSummary)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, RecordSummary>> GetSummariesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var summaries = new Dictionary<string, RecordSummary>(StringComparer.Ordinal);
        var wanted = ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (wanted.Length == 0)
        {
            return summaries;
        }

        foreach (var transaction in await _store.GetAsync(wanted, cancellationToken))
        {
            summaries[transaction.ItemId] = ToSummary(transaction);
        }

        return summaries;
    }

    private RecordSummary ToSummary(Transaction transaction)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        string[] title = [transaction.InvoiceNumber, transaction.Title];

        return new RecordSummary
        {
            Id = transaction.ItemId,
            Title = string.Join(" ", title.Where(part => !string.IsNullOrWhiteSpace(part))),
            Description = S["{0}, {1}", StatusName(transaction.Status), FormatAmount(transaction.TotalAmount, transaction.Currency)],
            AdminUrl = httpContext is null
                ? null
                : _linkGenerator.GetPathByAction(httpContext, DetailAction, DetailController, new { area = TransactionsConstants.Features.Area, itemId = transaction.ItemId }),
        };
    }

    private string StatusName(TransactionStatus status)
        => status switch
        {
            TransactionStatus.Pending => S["Pending"],
            TransactionStatus.Outstanding => S["Outstanding"],
            TransactionStatus.PartiallyPaid => S["Partially paid"],
            TransactionStatus.Paid => S["Paid"],
            TransactionStatus.Canceled => S["Canceled"],
            TransactionStatus.Failed => S["Failed"],
            TransactionStatus.Abandoned => S["Abandoned"],
            TransactionStatus.Refunded => S["Refunded"],
            _ => status.ToString(),
        };

    // The amount at the currency's precision after its code, as the transaction's invoice shows it.
    private static string FormatAmount(decimal amount, string currency)
        => string.IsNullOrEmpty(currency)
            ? CurrencyScale.Format(amount, currency)
            : $"{currency} {CurrencyScale.Format(amount, currency)}";
}
