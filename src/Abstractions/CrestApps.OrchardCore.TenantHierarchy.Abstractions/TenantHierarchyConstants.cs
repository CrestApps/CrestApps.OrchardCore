namespace CrestApps.OrchardCore.TenantHierarchy;

/// <summary>
/// Provides the constant values shared by every part of the tenant hierarchy.
/// </summary>
public static class TenantHierarchyConstants
{
    /// <summary>
    /// The YesSql collection that holds every tenant hierarchy document.
    /// </summary>
    public const string CollectionName = "TenantHierarchy";

    /// <summary>
    /// Contains the feature identifiers of the tenant hierarchy module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The module identifier.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.TenantHierarchy";

        /// <summary>
        /// The feature that runs in the Default tenant and manages parent tenants.
        /// </summary>
        public const string Platform = "CrestApps.OrchardCore.TenantHierarchy.Platform";

        /// <summary>
        /// The feature that runs in a parent tenant and manages its child tenants.
        /// </summary>
        public const string Parent = "CrestApps.OrchardCore.TenantHierarchy.Parent";

        /// <summary>
        /// The feature that runs in every child tenant. It is forced on through the tenant configuration.
        /// </summary>
        public const string Child = "CrestApps.OrchardCore.TenantHierarchy.Child";
    }

    /// <summary>
    /// Contains the role values a tenant can have in a hierarchy.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// A tenant that owns child tenants.
        /// </summary>
        public const string Parent = "Parent";

        /// <summary>
        /// A tenant that a parent tenant created.
        /// </summary>
        public const string Child = "Child";
    }

    /// <summary>
    /// Contains the shell settings keys the tenant hierarchy reads and writes. Tenant administrators cannot change
    /// shell settings, so these keys hold the host-controlled facts of the hierarchy.
    /// </summary>
    public static class SettingsKeys
    {
        /// <summary>
        /// The prefix of every tenant hierarchy key.
        /// </summary>
        public const string Prefix = "TenantHierarchy";

        /// <summary>
        /// The role of the tenant: <see cref="Roles.Parent"/> or <see cref="Roles.Child"/>.
        /// </summary>
        public const string Role = "TenantHierarchy:Role";

        /// <summary>
        /// The tenant identifier of the parent of a child tenant.
        /// </summary>
        public const string ParentTenantId = "TenantHierarchy:ParentTenantId";

        /// <summary>
        /// The tenant name of the parent of a child tenant. It is kept for lookup only.
        /// </summary>
        public const string ParentTenant = "TenantHierarchy:ParentTenant";

        /// <summary>
        /// The slug of the tenant. A parent slug is the first label of its host, a child slug the first label of its host.
        /// </summary>
        public const string Slug = "TenantHierarchy:Slug";

        /// <summary>
        /// The identifier of the parent's registry entry for a child tenant.
        /// </summary>
        public const string EntryId = "TenantHierarchy:EntryId";

        /// <summary>
        /// The display name of a parent tenant.
        /// </summary>
        public const string DisplayName = "TenantHierarchy:DisplayName";

        /// <summary>
        /// The section that holds the parent policy.
        /// </summary>
        public const string Policy = "TenantHierarchy:Policy";

        /// <summary>
        /// The section of a child tenant that lists the permissions denied to its local users.
        /// </summary>
        public const string DeniedLocalPermissions = "TenantHierarchy:DeniedLocalPermissions";

        /// <summary>
        /// The section of a child tenant that lists the features blocked by its parent policy.
        /// </summary>
        public const string BlockedFeatures = "TenantHierarchy:BlockedFeatures";

        /// <summary>
        /// The database strategy a child tenant was created with.
        /// </summary>
        public const string DatabaseStrategy = "TenantHierarchy:DatabaseStrategy";

        /// <summary>
        /// The database pool a child tenant was created in.
        /// </summary>
        public const string DatabasePool = "TenantHierarchy:DatabasePool";

        /// <summary>
        /// The database, schema or table prefix the provisioner created for a child tenant.
        /// </summary>
        public const string ProvisionedResource = "TenantHierarchy:ProvisionedResource";

        /// <summary>
        /// Marks a child tenant that was suspended together with its parent, so resuming the parent resumes it.
        /// </summary>
        public const string SuspendedWithParent = "TenantHierarchy:SuspendedWithParent";

        /// <summary>
        /// The section of a tenant configuration that lists the features that are always enabled.
        /// </summary>
        public const string Features = "Features";

        /// <summary>
        /// The tenant description key used by Orchard Core.
        /// </summary>
        public const string Description = "Description";

        /// <summary>
        /// The tenant category key used by Orchard Core.
        /// </summary>
        public const string Category = "Category";
    }

    /// <summary>
    /// Contains the claim types added to a principal that entered a child tenant through delegated access.
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// The delegated access session identifier.
        /// </summary>
        public const string SessionId = "th:sid";

        /// <summary>
        /// The parent tenant identifier.
        /// </summary>
        public const string ParentTenantId = "th:ptid";

        /// <summary>
        /// The parent user identifier.
        /// </summary>
        public const string ParentUserId = "th:puid";

        /// <summary>
        /// The display name of the parent tenant.
        /// </summary>
        public const string ParentDisplayName = "th:pname";

        /// <summary>
        /// The base address of the parent tenant.
        /// </summary>
        public const string ParentAddress = "th:purl";

        /// <summary>
        /// The singular label the parent uses for a child tenant.
        /// </summary>
        public const string ChildLabel = "th:lchild";

        /// <summary>
        /// How the tenant switcher shows the list of child tenants.
        /// </summary>
        public const string SwitcherMode = "th:smode";

        /// <summary>
        /// The version of the roles granted to the session.
        /// </summary>
        public const string RolesVersion = "th:rv";

        /// <summary>
        /// The interval, in seconds, at which the child validates the session with the parent.
        /// </summary>
        public const string ValidationInterval = "th:svi";

        /// <summary>
        /// A random identifier of a parent sign-in. Signing out of the parent ends every session it started.
        /// </summary>
        public const string ParentSessionId = "th:psid";

        /// <summary>
        /// The authentication methods reference claim.
        /// </summary>
        public const string AuthenticationMethods = "amr";
    }

    /// <summary>
    /// Contains the routes of the delegated access endpoints.
    /// </summary>
    public static class Routes
    {
        /// <summary>
        /// The parent endpoint that starts entering a child tenant.
        /// </summary>
        public const string Open = "delegated-access/open/{entryId}";

        /// <summary>
        /// The parent endpoint that issues a one-time code.
        /// </summary>
        public const string Authorize = "delegated-access/authorize";

        /// <summary>
        /// The child endpoint that starts the sign-in.
        /// </summary>
        public const string Enter = "delegated-access/enter";

        /// <summary>
        /// The child endpoint that redeems the one-time code.
        /// </summary>
        public const string Callback = "delegated-access/callback";

        /// <summary>
        /// The child endpoint that ends the delegated access session.
        /// </summary>
        public const string SignOut = "delegated-access/sign-out";

        /// <summary>
        /// The parent endpoint that renders the embedded tenant picker.
        /// </summary>
        public const string EmbeddedPicker = "delegated-access/picker";
    }

    /// <summary>
    /// Contains the names of the cookies the tenant hierarchy writes.
    /// </summary>
    public static class Cookies
    {
        /// <summary>
        /// The prefix that tells a browser a cookie is bound to one host, is secure and has the root path.
        /// </summary>
        public const string HostPrefix = "__Host-";

        /// <summary>
        /// The name of the short-lived cookie that holds the state of a delegated access sign-in in the child.
        /// </summary>
        public const string StateCookieName = "__Host-th_state";

        /// <summary>
        /// The name of the state cookie when the tenant is served over plain HTTP in development.
        /// </summary>
        public const string InsecureStateCookieName = "th_state";
    }
}
