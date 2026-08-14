using Microsoft.AspNetCore.Identity;
using OrchardCore.ContentManagement;
using OrchardCore.Security;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Provides user picker search results that include the resolved display name for each user.
/// </summary>
public sealed class DisplayNameUserPickerResultProvider : IUserPickerResultProvider
{
    private readonly RoleManager<IRole> _roleManager;
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayNameUserPickerResultProvider"/> class.
    /// </summary>
    /// <param name="roleManager">The role manager used to normalize role names for filtering.</param>
    /// <param name="userManager">The user manager used to normalize search terms.</param>
    /// <param name="session">The YesSql session used to query users.</param>
    /// <param name="displayNameProvider">The provider used to resolve each user's display name.</param>
    public DisplayNameUserPickerResultProvider(
        RoleManager<IRole> roleManager,
        UserManager<IUser> userManager,
        ISession session,
        IDisplayNameProvider displayNameProvider)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _session = session;
        _displayNameProvider = displayNameProvider;
    }

    /// <summary>
    /// Gets the name of this result provider.
    /// </summary>
    public string Name => "Default";

    /// <summary>
    /// Searches for users matching the given context and returns results with resolved display names.
    /// </summary>
    /// <param name="searchContext">The search context containing the query, roles, and display options.</param>
    public async Task<IEnumerable<UserPickerResult>> Search(UserPickerSearchContext searchContext)
    {
        var query = _session.Query<User>();

        if (!searchContext.DisplayAllUsers)
        {
            var roles = searchContext.Roles.Select(_roleManager.NormalizeKey);
            query = query.With<UserByRoleNameIndex>(x => x.RoleName.IsIn(roles));
        }

        if (!string.IsNullOrEmpty(searchContext.Query))
        {
            query = query.With<UserIndex>(x => x.NormalizedUserName.Contains(_userManager.NormalizeName(searchContext.Query)));
        }

        var users = await query.Take(50).ListAsync();

        var results = new List<UserPickerResult>();

        foreach (var user in users)
        {
            var displayName = await _displayNameProvider.GetAsync(user);

            results.Add(new UserPickerResult
            {
                UserId = user.UserId,
                DisplayText = displayName,
                IsEnabled = user.IsEnabled
            });
        }

        return results.OrderBy(x => x.DisplayText);
    }
}
