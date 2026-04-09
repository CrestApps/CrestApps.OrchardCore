using Microsoft.AspNetCore.Identity;
using OrchardCore.ContentManagement;
using OrchardCore.Security;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

public sealed class DisplayNameUserPickerResultProvider : IUserPickerResultProvider
{
    private readonly RoleManager<IRole> _roleManager;
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;

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

    public string Name => "Default";

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
