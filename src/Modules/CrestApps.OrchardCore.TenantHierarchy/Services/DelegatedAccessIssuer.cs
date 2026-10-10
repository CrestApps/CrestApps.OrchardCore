using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// The parent side of delegated access: it works out which child tenants a parent user may enter, issues one-time
/// codes and ends sessions.
/// </summary>
public sealed class DelegatedAccessIssuer
{
    /// <summary>
    /// How long a one-time code is valid.
    /// </summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(60);

    private readonly ITenantHierarchyBroker _broker;
    private readonly ChildTenantEntryStore _entryStore;
    private readonly AccessGrantStore _grantStore;
    private readonly DelegatedAccessCodeStore _codeStore;
    private readonly DelegatedAccessSessionStore _sessionStore;
    private readonly HierarchyAuditLog _auditLog;
    private readonly UserManager<IUser> _userManager;
    private readonly ShellSettings _shellSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessIssuer"/> class.
    /// </summary>
    /// <param name="broker">The tenant hierarchy broker.</param>
    /// <param name="entryStore">The registry store.</param>
    /// <param name="grantStore">The access grant store.</param>
    /// <param name="codeStore">The one-time code store.</param>
    /// <param name="sessionStore">The delegated access session store.</param>
    /// <param name="auditLog">The hierarchy activity log.</param>
    /// <param name="userManager">The user manager of the parent tenant.</param>
    /// <param name="shellSettings">The settings of the parent tenant.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    public DelegatedAccessIssuer(
        ITenantHierarchyBroker broker,
        ChildTenantEntryStore entryStore,
        AccessGrantStore grantStore,
        DelegatedAccessCodeStore codeStore,
        DelegatedAccessSessionStore sessionStore,
        HierarchyAuditLog auditLog,
        UserManager<IUser> userManager,
        ShellSettings shellSettings,
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
    {
        _broker = broker;
        _entryStore = entryStore;
        _grantStore = grantStore;
        _codeStore = codeStore;
        _sessionStore = sessionStore;
        _auditLog = auditLog;
        _userManager = userManager;
        _shellSettings = shellSettings;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    /// <summary>
    /// Lists the child tenants a parent user may enter now.
    /// </summary>
    /// <param name="principal">The parent principal.</param>
    public async Task<IReadOnlyList<ChildTenantInfo>> GetEnterableChildrenAsync(ClaimsPrincipal principal)
    {
        var user = await GetEnabledUserAsync(principal);

        if (user is null)
        {
            return [];
        }

        var grants = await _grantStore.ListForPrincipalAsync(user.UserId, user.RoleNames);
        var children = await _broker.ListChildrenAsync();
        var enterable = AccessGrantResolver
            .ResolveEnterableEntries(grants, children.Select(child => child.Entry.EntryId))
            .ToHashSet(StringComparer.Ordinal);

        return children
            .Where(child => child.CanEnter && enterable.Contains(child.Entry.EntryId))
            .ToList();
    }

    /// <summary>
    /// Returns the child roles a parent user would get in a child tenant. An empty result means the user may not enter it.
    /// </summary>
    /// <param name="principal">The parent principal.</param>
    /// <param name="entryId">The registry entry of the child tenant.</param>
    public async Task<string[]> GetGrantedRolesAsync(ClaimsPrincipal principal, string entryId)
    {
        var user = await GetEnabledUserAsync(principal);

        if (user is null || string.IsNullOrEmpty(entryId))
        {
            return [];
        }

        var grants = await _grantStore.ListForPrincipalAsync(user.UserId, user.RoleNames);

        return AccessGrantResolver.ResolveChildRoles(grants, entryId);
    }

    /// <summary>
    /// Returns whether the parent policy requires multi-factor authentication and the principal did not use it.
    /// </summary>
    /// <param name="principal">The parent principal.</param>
    public bool IsMfaRequiredAndMissing(ClaimsPrincipal principal)
    {
        if (!_shellSettings.GetParentPolicy().RequireMfa)
        {
            return false;
        }

        return principal?.FindAll(TenantHierarchyConstants.ClaimTypes.AuthenticationMethods)
            .Any(claim => string.Equals(claim.Value, "mfa", StringComparison.OrdinalIgnoreCase)) != true;
    }

    /// <summary>
    /// Issues a one-time code for a child tenant and returns the callback address of the child, or
    /// <see langword="null"/> when the user may not enter the child tenant. The callback address is built from the
    /// child's shell settings, never from the request.
    /// </summary>
    /// <param name="principal">The parent principal.</param>
    /// <param name="childTenantId">The tenant identifier the child sent.</param>
    /// <param name="codeChallenge">The PKCE code challenge.</param>
    /// <param name="state">The state the child sent. It is returned to the child unchanged.</param>
    public async Task<string> IssueCodeAsync(ClaimsPrincipal principal, string childTenantId, string codeChallenge, string state)
    {
        if (string.IsNullOrEmpty(childTenantId) || string.IsNullOrEmpty(codeChallenge) || string.IsNullOrEmpty(state) ||
            codeChallenge.Length < 43 || codeChallenge.Length > 128 || state.Length > 512)
        {
            return null;
        }

        var user = await GetEnabledUserAsync(principal);
        var entry = await _entryStore.FindByTenantIdAsync(childTenantId);

        if (user is null || entry is null)
        {
            return null;
        }

        var child = await _broker.GetChildAsync(entry.EntryId);

        if (child is null || !child.CanEnter || string.IsNullOrEmpty(child.Address))
        {
            await RecordRefusalAsync(user, entry);

            return null;
        }

        var roles = await GetGrantedRolesAsync(principal, entry.EntryId);

        if (roles.Length == 0 || IsMfaRequiredAndMissing(principal))
        {
            await RecordRefusalAsync(user, entry);

            return null;
        }

        var code = DelegatedAccessTokens.CreateToken();
        var now = _clock.UtcNow;

        await _codeStore.CreateAsync(new DelegatedAccessCode
        {
            CodeHash = DelegatedAccessTokens.Hash(code),
            ChildEntryId = entry.EntryId,
            ChildTenantId = entry.TenantId,
            ParentUserId = user.UserId,
            ParentSessionId = principal.FindFirst(TenantHierarchyConstants.ClaimTypes.ParentSessionId)?.Value,
            CodeChallenge = codeChallenge,
            AuthenticationMethods = principal.FindAll(TenantHierarchyConstants.ClaimTypes.AuthenticationMethods).Select(claim => claim.Value).Distinct().ToArray(),
            IpAddress = GetIpAddress(),
            CreatedUtc = now,
            ExpiresUtc = now.Add(CodeLifetime),
        });

        return $"{child.Address}/{TenantHierarchyConstants.Routes.Callback}{QueryString.Create(new Dictionary<string, string>
        {
            ["code"] = code,
            ["state"] = state,
        })}";
    }

    /// <summary>
    /// Returns the address of a child tenant of this parent, from its shell settings, or <see langword="null"/> when
    /// the tenant is not one of its child tenants.
    /// </summary>
    /// <param name="childTenantId">The tenant identifier of the child tenant.</param>
    public async Task<string> GetChildAddressAsync(string childTenantId)
    {
        if (string.IsNullOrEmpty(childTenantId))
        {
            return null;
        }

        var entry = await _entryStore.FindByTenantIdAsync(childTenantId);

        return entry is null
            ? null
            : (await _broker.GetChildAsync(entry.EntryId))?.Address;
    }

    /// <summary>
    /// Ends every open session a parent sign-in started.
    /// </summary>
    /// <param name="parentSessionId">The parent sign-in.</param>
    public async Task<int> EndSessionsOfSignInAsync(string parentSessionId)
    {
        if (string.IsNullOrEmpty(parentSessionId))
        {
            return 0;
        }

        return await EndAsync(await _sessionStore.ListOpenByParentSessionAsync(parentSessionId), DelegatedSessionRules.SignedOut);
    }

    /// <summary>
    /// Ends every open session of a parent user.
    /// </summary>
    /// <param name="userId">The parent user.</param>
    public async Task<int> EndAllSessionsAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        return await EndAsync(await _sessionStore.ListOpenByUserAsync(userId), DelegatedSessionRules.SignedOutEverywhere);
    }

    private async Task<int> EndAsync(IEnumerable<DelegatedAccessSession> sessions, string reason)
    {
        var count = 0;

        foreach (var session in sessions)
        {
            session.EndedUtc = _clock.UtcNow;
            session.EndReason = reason;
            await _sessionStore.SaveAsync(session);
            await _auditLog.RecordAsync(new HierarchyAuditEvent
            {
                Name = HierarchyAuditEventNames.SessionEnded,
                ChildEntryId = session.ChildEntryId,
                UserId = session.ParentUserId,
                UserName = session.ParentUserName,
                IpAddress = GetIpAddress(),
                SessionReference = DelegatedAccessTokens.GetReference(session.SessionHash),
                Details = reason,
            });

            count++;
        }

        return count;
    }

    private Task RecordRefusalAsync(User user, ChildTenantEntry entry)
    {
        return _auditLog.RecordAsync(new HierarchyAuditEvent
        {
            Name = HierarchyAuditEventNames.EntryRefused,
            ChildEntryId = entry.EntryId,
            ChildDisplayName = entry.DisplayName,
            UserId = user.UserId,
            UserName = user.UserName,
            IpAddress = GetIpAddress(),
        });
    }

    private async Task<User> GetEnabledUserAsync(ClaimsPrincipal principal)
    {
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        return await _userManager.FindByIdAsync(userId) is User { IsEnabled: true } user
            ? user
            : null;
    }

    private string GetIpAddress()
        => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
