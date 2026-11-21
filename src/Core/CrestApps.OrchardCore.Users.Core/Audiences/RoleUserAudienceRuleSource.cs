using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core.Audiences;

/// <summary>
/// An audience rule that matches the users in any of some roles.
/// </summary>
public sealed class RoleUserAudienceRuleSource : IUserAudienceRuleSource
{
    /// <summary>
    /// The name of the source.
    /// </summary>
    public const string SourceName = "Role";

    private readonly IRoleService _roleService;
    private readonly UserManager<IUser> _userManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleUserAudienceRuleSource"/> class.
    /// </summary>
    public RoleUserAudienceRuleSource(IRoleService roleService, UserManager<IUser> userManager, IStringLocalizer<RoleUserAudienceRuleSource> stringLocalizer)
    {
        _roleService = roleService;
        _userManager = userManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => SourceName;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["In a role"];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _roleService.GetRoleNamesAsync()).Order(StringComparer.CurrentCultureIgnoreCase).Select(role => new UserAudienceRuleOption(role, role)).ToArray();

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in rule.Values)
        {
            foreach (var user in await _userManager.GetUsersInRoleAsync(role))
            {
                ids.Add(await _userManager.GetUserIdAsync(user));
            }
        }

        return ids;
    }

    /// <inheritdoc/>
    public async Task<bool> IsMatchAsync(UserAudienceRule rule, string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return false;
        }

        foreach (var role in rule.Values)
        {
            if (await _userManager.IsInRoleAsync(user, role))
            {
                return true;
            }
        }

        return false;
    }
}
