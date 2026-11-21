using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.DataConnectors.Http;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

/// <summary>
/// Decides where an Azure AI Search data source may sign in as the host. The host's identity can read whatever the host
/// can, other tenants' indexes included, so, as for the SQL Server connector's host sign-in, it works on the default
/// tenant, and on another tenant only for a search service, index and identity listed in
/// <see cref="AzureAISearchHostIdentityOptions"/> (review 10, B1).
/// </summary>
internal sealed class AzureAISearchHostIdentity
{
    /// <summary>
    /// The error shown when a data source signs in as the host where it may not.
    /// </summary>
    public const string NotApprovedError = "Signing in as the site's host is only available on the default tenant, or for a search service and index the host approved for this tenant. Use an API key, or ask the host to approve this index.";

    private readonly bool _isDefaultTenant;
    private readonly List<AzureAISearchHostIdentityIndex> _approved;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchHostIdentity"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="options">The host's approvals for this tenant.</param>
    public AzureAISearchHostIdentity(ShellSettings shellSettings, IOptions<AzureAISearchHostIdentityOptions> options)
    {
        _isDefaultTenant = shellSettings?.IsDefaultShell() == true;
        _approved = options?.Value?.HostIdentityIndexes ?? [];
    }

    /// <summary>
    /// Whether a data source's own settings sign in as the host rather than with a key the tenant entered.
    /// </summary>
    /// <param name="metadata">The data source's settings.</param>
    public static bool SignsInAsHost(AzureAISearchSourceMetadata metadata)
        => !string.Equals(metadata.GetAuthenticationType(), AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a data connection signs in as the host: one without authentication uses the default Azure credential.
    /// </summary>
    /// <param name="settings">The connection's HTTP settings.</param>
    public static bool SignsInAsHost(HttpConnectionSettings settings)
        => (settings.Authentication?.Type ?? HttpAuthenticationType.None) == HttpAuthenticationType.None;

    /// <summary>
    /// Whether this tenant may read the index as the host.
    /// </summary>
    /// <param name="endpoint">The search service endpoint.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="identityClientId">The managed identity's client ID, or nothing.</param>
    public bool IsApproved(string endpoint, string indexName, string identityClientId)
        => _isDefaultTenant || _approved.Any(entry =>
            entry is not null &&
            !string.IsNullOrWhiteSpace(entry.Endpoint) &&
            !string.IsNullOrWhiteSpace(entry.IndexName) &&
            SecretDestination.SameOrigin(entry.Endpoint, endpoint) &&
            SameText(entry.IndexName, indexName) &&
            SameText(entry.IdentityClientId, identityClientId));

    private static bool SameText(string approved, string used)
        => string.Equals(approved?.Trim() ?? string.Empty, used?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
