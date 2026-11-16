using System.Text.Json;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.Learning.Lti.Services;

/// <summary>
/// Keeps each external tool's OpenID application in step with the tool: the client id, a confidential client type, the
/// redirect URIs, the key set (the tool's own, or a copy of what its JWKS URL served when the tool was saved, because
/// OpenIddict requires a confidential client to have a secret or a key set), and the permissions of the token endpoint,
/// the client credentials grant and the LTI scopes the tool may use. The editor hands the redirect URIs and the key set
/// over for the request; they are written once the tool is saved.
/// </summary>
public sealed class ExternalToolClients
{
    /// <summary>
    /// The application property that marks an application made by External Tools.
    /// </summary>
    public const string MarkerProperty = "CrestApps.Learning.Lti";

    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

    private readonly IOpenIdApplicationManager _applications;
    private readonly Dictionary<string, PendingClient> _pending = new(StringComparer.Ordinal);

    public ExternalToolClients(IOpenIdApplicationManager applications)
    {
        _applications = applications;
    }

    /// <summary>
    /// Reads the redirect URIs and the key set of a tool's application.
    /// </summary>
    public async Task<(IReadOnlyList<string> RedirectUris, string KeySet)> ReadAsync(string clientId)
    {
        var application = string.IsNullOrEmpty(clientId) ? null : await _applications.FindByClientIdAsync(clientId);

        if (application is null)
        {
            return ([], null);
        }

        var redirectUris = await _applications.GetRedirectUrisAsync(application);
        var keySet = await _applications.GetJsonWebKeySetAsync(application);

        return (redirectUris.ToArray(), keySet is null ? null : JsonSerializer.Serialize(keySet, _indented));
    }

    /// <summary>
    /// Holds the redirect URIs and the key set (as the JSON entered, or empty to read the tool's JWKS URL) the editor
    /// bound, until the tool is checked and saved.
    /// </summary>
    public void SetPending(ExternalTool tool, IReadOnlyList<string> redirectUris, string keySet)
        => _pending[tool.ItemId] = new PendingClient(redirectUris, keySet);

    /// <summary>
    /// Returns what the editor bound for a tool in this request, when it did.
    /// </summary>
    public bool TryGetPending(string toolId, out PendingClient pending)
        => _pending.TryGetValue(toolId ?? string.Empty, out pending);

    /// <summary>
    /// Writes the tool's application, made when it does not exist.
    /// </summary>
    public async Task SyncAsync(ExternalTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var application = await _applications.FindByClientIdAsync(tool.ClientId);
        var descriptor = new OpenIdApplicationDescriptor();

        if (application is not null)
        {
            await _applications.PopulateAsync(descriptor, application);
        }

        descriptor.ClientId = tool.ClientId;
        descriptor.DisplayName = tool.Name;
        descriptor.ClientType = OpenIddictConstants.ClientTypes.Confidential;
        descriptor.ConsentType = OpenIddictConstants.ConsentTypes.Implicit;
        descriptor.Properties[MarkerProperty] = JsonSerializer.SerializeToElement(true);

        if (_pending.TryGetValue(tool.ItemId, out var pending))
        {
            descriptor.RedirectUris.Clear();

            foreach (var uri in pending.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri, UriKind.Absolute));
            }

            descriptor.JsonWebKeySet = pending.KeySet;
        }

        descriptor.Permissions.RemoveWhere(permission => permission.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.Ordinal));
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

        foreach (var scope in ScopesOf(tool))
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        if (application is null)
        {
            await _applications.CreateAsync(descriptor);
        }
        else
        {
            await _applications.UpdateAsync(application, descriptor);
        }
    }

    /// <summary>
    /// Removes a tool's application, when External Tools made it.
    /// </summary>
    public async Task DeleteAsync(string clientId)
    {
        var application = string.IsNullOrEmpty(clientId) ? null : await _applications.FindByClientIdAsync(clientId);

        if (application is null)
        {
            return;
        }

        var properties = await _applications.GetPropertiesAsync(application);

        if (properties.ContainsKey(MarkerProperty))
        {
            await _applications.DeleteAsync(application);
        }
    }

    /// <summary>
    /// Returns the scopes a tool may ask for.
    /// </summary>
    public static IEnumerable<string> ScopesOf(ExternalTool tool)
    {
        if (tool.AllowGrades)
        {
            yield return LtiConstants.Scopes.LineItem;
            yield return LtiConstants.Scopes.LineItemReadOnly;
            yield return LtiConstants.Scopes.ResultReadOnly;
            yield return LtiConstants.Scopes.Score;
        }

        if (tool.AllowNamesAndRoles)
        {
            yield return LtiConstants.Scopes.ContextMembershipReadOnly;
        }
    }

    /// <summary>
    /// What the editor bound for a tool's application: the redirect URIs, the key set as entered, and the key set the
    /// tool's handler resolved from it (or from the JWKS URL) once it passed.
    /// </summary>
    public sealed class PendingClient
    {
        internal PendingClient(IReadOnlyList<string> redirectUris, string keySetJson)
        {
            RedirectUris = redirectUris ?? [];
            KeySetJson = keySetJson;
        }

        /// <summary>
        /// Gets the redirect URIs.
        /// </summary>
        public IReadOnlyList<string> RedirectUris { get; }

        /// <summary>
        /// Gets the key set as entered; empty when the tool's JWKS URL is to be read.
        /// </summary>
        public string KeySetJson { get; }

        /// <summary>
        /// Gets or sets the key set written to the application.
        /// </summary>
        public JsonWebKeySet KeySet { get; set; }
    }
}
