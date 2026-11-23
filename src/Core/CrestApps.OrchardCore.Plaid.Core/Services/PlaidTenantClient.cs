using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// The <see cref="IPlaidClient"/> over <see cref="PlaidClient"/>, with the credentials of
/// <see cref="IPlaidCredentialsProvider"/>.
/// </summary>
public sealed class PlaidTenantClient : IPlaidClient
{
    private readonly PlaidClient _client;
    private readonly IPlaidCredentialsProvider _credentialsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidTenantClient"/> class.
    /// </summary>
    /// <param name="client">The Plaid client.</param>
    /// <param name="credentialsProvider">The tenant's credentials.</param>
    public PlaidTenantClient(PlaidClient client, IPlaidCredentialsProvider credentialsProvider)
    {
        _client = client;
        _credentialsProvider = credentialsProvider;
    }

    /// <inheritdoc />
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        => await _credentialsProvider.GetAsync(cancellationToken) is not null;

    /// <inheritdoc />
    public async Task<PlaidLinkTokenResponse> CreateLinkTokenAsync(PlaidLinkTokenRequest request, CancellationToken cancellationToken = default)
        => await _client.CreateLinkTokenAsync(await GetCredentialsAsync(cancellationToken), request, cancellationToken);

    /// <inheritdoc />
    public async Task<PlaidPublicTokenExchangeResponse> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken = default)
        => await _client.ExchangePublicTokenAsync(await GetCredentialsAsync(cancellationToken), publicToken, cancellationToken);

    /// <inheritdoc />
    public async Task<PlaidAccountsResponse> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default)
        => await _client.GetAccountsAsync(await GetCredentialsAsync(cancellationToken), accessToken, cancellationToken);

    /// <inheritdoc />
    public async Task<PlaidTransactionsSyncResponse> SyncTransactionsAsync(PlaidTransactionsSyncRequest request, CancellationToken cancellationToken = default)
        => await _client.SyncTransactionsAsync(await GetCredentialsAsync(cancellationToken), request, cancellationToken);

    /// <inheritdoc />
    public async Task<PlaidInstitution> GetInstitutionAsync(string institutionId, IEnumerable<string> countryCodes = null, CancellationToken cancellationToken = default)
        => await _client.GetInstitutionAsync(await GetCredentialsAsync(cancellationToken), institutionId, countryCodes, cancellationToken);

    /// <inheritdoc />
    public async Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken = default)
        => await _client.RemoveItemAsync(await GetCredentialsAsync(cancellationToken), accessToken, cancellationToken);

    private async Task<PlaidCredentials> GetCredentialsAsync(CancellationToken cancellationToken)
        => await _credentialsProvider.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("Plaid is not set up on this site: no Plaid credentials are configured.");
}
