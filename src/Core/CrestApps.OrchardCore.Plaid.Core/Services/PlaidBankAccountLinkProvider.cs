using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.SensitiveData;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Links a bank with Plaid Link and Plaid Auth: a link token opens Plaid Link in the browser, the public token it returns
/// is exchanged for an access token, and <c>/auth/get</c> gives the account's ACH numbers. The item is removed at once,
/// unless the account number is one the bank made for Plaid; then it is kept, its access token protected, so the
/// account can be taken back to unverified if the person takes back Plaid's access.
/// </summary>
public sealed class PlaidBankAccountLinkProvider : IBankAccountLinkProvider
{
    private static readonly JsonSerializerOptions _clientResultOptions = new(JsonSerializerDefaults.Web);

    private readonly IPlaidCredentialsProvider _credentialsProvider;
    private readonly IPlaidLinkSite _linkSite;
    private readonly PlaidClient _client;
    private readonly IPlaidItemStore _store;
    private readonly PlaidItemCleaner _cleaner;
    private readonly ISensitiveValueProtector _protector;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidBankAccountLinkProvider"/> class.
    /// </summary>
    public PlaidBankAccountLinkProvider(
        IPlaidCredentialsProvider credentialsProvider,
        IPlaidLinkSite linkSite,
        PlaidClient client,
        IPlaidItemStore store,
        PlaidItemCleaner cleaner,
        ISensitiveValueProtector protector,
        IClock clock,
        ILogger<PlaidBankAccountLinkProvider> logger,
        IStringLocalizer<PlaidBankAccountLinkProvider> stringLocalizer)
    {
        _credentialsProvider = credentialsProvider;
        _linkSite = linkSite;
        _client = client;
        _store = store;
        _cleaner = cleaner;
        _protector = protector;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name
        => PlaidConstants.ProviderName;

    /// <inheritdoc />
    public LocalizedString DisplayName
        => S["Plaid"];

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => await _credentialsProvider.GetAsync(cancellationToken) is not null;

    /// <inheritdoc />
    public async Task<BankAccountLinkStart> StartAsync(BankAccountLinkContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsUnitedStates(context.Account))
        {
            return BankAccountLinkStart.Refused(S["Plaid links banks in the United States only."]);
        }

        if (string.IsNullOrEmpty(context.UserId))
        {
            return BankAccountLinkStart.Refused(S["Sign in to link a bank."]);
        }

        if (await _credentialsProvider.GetAsync(cancellationToken) is not { } credentials)
        {
            return BankAccountLinkStart.Refused(S["Plaid is not set up on this site."]);
        }

        try
        {
            var request = await _linkSite.CreateLinkTokenRequestAsync(context.UserId, cancellationToken);
            var token = await _client.CreateLinkTokenAsync(credentials, request, cancellationToken);

            return string.IsNullOrEmpty(token.LinkToken)
                ? BankAccountLinkStart.Refused(S["Plaid did not answer. Try again in a moment."])
                : BankAccountLinkStart.Ready(PlaidConstants.LinkShapeType, token.LinkToken);
        }
        catch (PlaidApiException exception)
        {
            _logger.LogWarning("Plaid refused to make a link token: {Error}", exception.Message);

            return BankAccountLinkStart.Refused(Describe(exception));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Plaid could not be reached to make a link token.");

            return BankAccountLinkStart.Refused(S["Plaid did not answer. Try again in a moment."]);
        }
    }

    /// <inheritdoc />
    public async Task<BankAccountLinkResult> CompleteAsync(BankAccountLinkContext context, string clientResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsUnitedStates(context.Account))
        {
            return BankAccountLinkResult.Refused(S["Plaid links banks in the United States only."]);
        }

        var linked = Parse(clientResult);

        if (linked is null)
        {
            return BankAccountLinkResult.Refused(S["The bank was not linked. Try again."]);
        }

        if (await _credentialsProvider.GetAsync(cancellationToken) is not { } credentials)
        {
            return BankAccountLinkResult.Refused(S["Plaid is not set up on this site."]);
        }

        PlaidPublicTokenExchangeResponse exchange;

        try
        {
            exchange = await _client.ExchangePublicTokenAsync(credentials, linked.PublicToken, cancellationToken);
        }
        catch (PlaidApiException exception)
        {
            _logger.LogWarning("Plaid refused to exchange a public token: {Error}", exception.Message);

            return BankAccountLinkResult.Refused(Describe(exception));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The exchange may have happened; the item is unknown here, so it can only be removed from the dashboard.
            _logger.LogWarning(exception, "Plaid could not be reached to exchange a public token.");

            return BankAccountLinkResult.Refused(S["Plaid did not answer. Try again in a moment."]);
        }

        try
        {
            var auth = await _client.GetAuthAsync(credentials, exchange.AccessToken, linked.AccountId, cancellationToken);
            var numbers = auth.Numbers?.Ach?.FirstOrDefault(entry => string.Equals(entry.AccountId, linked.AccountId, StringComparison.Ordinal));
            var account = auth.Accounts?.FirstOrDefault(entry => string.Equals(entry.AccountId, linked.AccountId, StringComparison.Ordinal));

            if (numbers is null || string.IsNullOrEmpty(numbers.Account) || string.IsNullOrEmpty(numbers.Routing))
            {
                await _cleaner.RemoveAtPlaidAsync(exchange.AccessToken, exchange.ItemId, cancellationToken);

                return BankAccountLinkResult.Refused(S["The bank did not give this account's numbers. Enter them yourself instead."]);
            }

            return new BankAccountLinkResult
            {
                Succeeded = true,
                RoutingNumber = numbers.Routing,
                AccountNumber = numbers.Account,
                Kind = account?.Subtype switch
                {
                    "checking" => BankAccountKind.Checking,
                    "savings" => BankAccountKind.Savings,
                    _ => null,
                },
                BankName = string.IsNullOrWhiteSpace(linked.InstitutionName) ? null : linked.InstitutionName.Trim(),
                Mask = account?.Mask,
                Status = BankAccountStatus.Verified,
                ProviderState = new PlaidLinkState
                {
                    ItemId = exchange.ItemId,
                    AccessToken = exchange.AccessToken,
                    PlaidAccountId = linked.AccountId,
                    IsTokenized = numbers.IsTokenizedAccountNumber,
                    Environment = credentials.Environment,
                },
            };
        }
        catch (PlaidApiException exception)
        {
            _logger.LogWarning("Plaid refused to give the numbers of item {ItemId}: {Error}", exchange.ItemId, exception.Message);
            await _cleaner.RemoveAtPlaidAsync(exchange.AccessToken, exchange.ItemId, cancellationToken);

            return BankAccountLinkResult.Refused(exception.ErrorCode == "PRODUCT_NOT_READY"
                ? S["The bank has not confirmed this account yet. Enter the numbers yourself instead."]
                : Describe(exception));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Plaid could not be reached to give the numbers of item {ItemId}.", exchange.ItemId);
            await _cleaner.RemoveAtPlaidAsync(exchange.AccessToken, exchange.ItemId, CancellationToken.None);

            return BankAccountLinkResult.Refused(S["Plaid did not answer. Try again in a moment."]);
        }
    }

    /// <inheritdoc />
    public async Task FinishAsync(BankAccountLinkContext context, BankAccountLinkResult result, bool saved, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        if (result.ProviderState is not PlaidLinkState state)
        {
            return;
        }

        if (!saved)
        {
            await _cleaner.RemoveAtPlaidAsync(state.AccessToken, state.ItemId, cancellationToken);

            return;
        }

        // A new link replaces what an earlier one kept.
        await _cleaner.RemoveForBankAccountAsync(context.Account.ItemId, exceptItemId: state.ItemId, cancellationToken);

        if (!state.IsTokenized)
        {
            await _cleaner.RemoveAtPlaidAsync(state.AccessToken, state.ItemId, cancellationToken);

            return;
        }

        await _store.SaveAsync(new PlaidItem
        {
            ItemId = state.ItemId,
            PlaidAccountId = state.PlaidAccountId,
            BankAccountId = context.Account.ItemId,
            AccessToken = _protector.Protect(PlaidConstants.AccessTokenPurpose, state.AccessToken, visibleCharacters: 0),
            Environment = state.Environment,
            CreatedUtc = _clock.UtcNow,
        }, cancellationToken);
    }

    private string Describe(PlaidApiException exception)
        => !string.IsNullOrWhiteSpace(exception.Error.DisplayMessage)
            ? exception.Error.DisplayMessage
            : exception.Error.ErrorType switch
            {
                "INVALID_INPUT" or "INVALID_REQUEST" => S["Plaid is not set up correctly on this site. Ask an administrator to check the Plaid settings."],
                "RATE_LIMIT_EXCEEDED" => S["Too many tries. Wait a minute and try again."],
                "INSTITUTION_ERROR" => S["The bank is not answering Plaid right now. Try again later or enter the numbers yourself."],
                _ => S["Plaid could not link the bank. Try again or enter the numbers yourself."],
            };

    private static bool IsUnitedStates(BankAccount account)
        => string.Equals(account?.Country ?? BankingConstants.Countries.UnitedStates, BankingConstants.Countries.UnitedStates, StringComparison.OrdinalIgnoreCase);

    private static PlaidLinkClientResult Parse(string clientResult)
    {
        if (string.IsNullOrWhiteSpace(clientResult) || clientResult.Length > 4096)
        {
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<PlaidLinkClientResult>(clientResult, _clientResultOptions);

            return result is null || string.IsNullOrEmpty(result.PublicToken) || string.IsNullOrEmpty(result.AccountId) ? null : result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// What the Plaid Link script posts.
    /// </summary>
    internal sealed class PlaidLinkClientResult
    {
        [JsonPropertyName("publicToken")]
        public string PublicToken { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; }

        [JsonPropertyName("institutionName")]
        public string InstitutionName { get; set; }
    }

    /// <summary>
    /// What the provider keeps between completing a link and finishing it. In memory only.
    /// </summary>
    internal sealed class PlaidLinkState
    {
        public string ItemId { get; set; }

        public string AccessToken { get; set; }

        public string PlaidAccountId { get; set; }

        public bool IsTokenized { get; set; }

        public PlaidEnvironment Environment { get; set; }

        public override string ToString()
            => $"Item {ItemId}";
    }
}
