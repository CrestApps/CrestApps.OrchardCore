using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// The only code path that reaches another tenant of a hierarchy. The caller is always the tenant whose code is
/// running, identified by its own shell settings and never by request input. The other side is resolved from
/// host-controlled settings only, and the parent-child link must hold in both directions. No authorization or other
/// code that depends on the current HTTP request runs inside the other tenant's scope.
/// </summary>
public interface ITenantHierarchyBroker
{
    /// <summary>
    /// Lists the child tenants of the current parent tenant, joined with their live state.
    /// </summary>
    Task<IReadOnlyList<ChildTenantInfo>> ListChildrenAsync();

    /// <summary>
    /// Returns a child tenant of the current parent tenant, or <see langword="null"/> when the entry does not exist.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    Task<ChildTenantInfo> GetChildAsync(string entryId);

    /// <summary>
    /// Returns whether a host is already used by any tenant of the application. It reveals nothing else.
    /// </summary>
    /// <param name="host">The host.</param>
    Task<bool> IsHostInUseAsync(string host);

    /// <summary>
    /// Writes the shell settings of a new child tenant of the current parent tenant. The registry entry must already
    /// be saved with <see cref="ChildTenantStatus.Provisioning"/>.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    /// <param name="database">The database settings produced by the provisioner.</param>
    Task<TenantHierarchyResult> CreateChildSettingsAsync(string entryId, ChildDatabaseProvisioningResult database);

    /// <summary>
    /// Runs the setup of a child tenant of the current parent tenant. It needs an HTTP context, so it normally runs
    /// in a background job after the request that created the tenant.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    Task<TenantHierarchyResult> SetupChildAsync(string entryId);

    /// <summary>
    /// Updates the slug, display name and description of a child tenant of the current parent tenant.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    Task<TenantHierarchyResult> UpdateChildSettingsAsync(string entryId);

    /// <summary>
    /// Disables a child tenant of the current parent tenant.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    Task<TenantHierarchyResult> SuspendChildAsync(string entryId);

    /// <summary>
    /// Enables a suspended child tenant of the current parent tenant.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    Task<TenantHierarchyResult> ResumeChildAsync(string entryId);

    /// <summary>
    /// Reloads a child tenant of the current parent tenant.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    Task<TenantHierarchyResult> ReloadChildAsync(string entryId);

    /// <summary>
    /// Removes a suspended or uninitialized child tenant of the current parent tenant, then removes its database
    /// through the provisioner.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    Task<TenantHierarchyResult> RemoveChildAsync(string entryId);

    /// <summary>
    /// Lists the features a child tenant of the current parent tenant can enable, with their state.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    Task<IReadOnlyList<ChildFeatureInfo>> GetChildFeaturesAsync(string entryId);

    /// <summary>
    /// Enables or disables features in a child tenant of the current parent tenant. Features the child may not
    /// enable and features that are always enabled are ignored.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    /// <param name="featureIds">The feature identifiers.</param>
    /// <param name="enable"><see langword="true"/> to enable the features, <see langword="false"/> to disable them.</param>
    Task<TenantHierarchyResult> SetChildFeaturesAsync(string entryId, IEnumerable<string> featureIds, bool enable);

    /// <summary>
    /// Lists the role names of a child tenant of the current parent tenant, for the access grant editor.
    /// </summary>
    /// <param name="entryId">The registry entry identifier, or <see langword="null"/> for the roles of the setup recipes.</param>
    Task<IReadOnlyList<string>> GetChildRolesAsync(string entryId);

    /// <summary>
    /// Returns the base address of the parent of the current child tenant.
    /// </summary>
    Task<string> GetParentAddressAsync();

    /// <summary>
    /// Redeems a one-time code for the current child tenant. Returns <see langword="null"/> when the code is unknown,
    /// expired, already used, issued for another tenant or does not match the verifier.
    /// </summary>
    /// <param name="code">The one-time code.</param>
    /// <param name="codeVerifier">The PKCE code verifier.</param>
    Task<DelegatedAccessRedemption> RedeemCodeAsync(string code, string codeVerifier);

    /// <summary>
    /// Validates a delegated access session of the current child tenant with its parent.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    Task<DelegatedSessionValidation> ValidateSessionAsync(string sessionId);

    /// <summary>
    /// Ends a delegated access session of the current child tenant.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="reason">Why the session ended.</param>
    Task EndSessionAsync(string sessionId, string reason);
}
