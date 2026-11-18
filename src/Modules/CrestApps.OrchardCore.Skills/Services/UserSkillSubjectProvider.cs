using System.Security.Claims;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Describes users to Skills: their display names. The Skills permissions alone decide who may change a user's skills
/// and certificates, as on the Certificates page.
/// </summary>
internal sealed class UserSkillSubjectProvider : ISkillSubjectProvider
{
    private readonly IUserSearchService _userSearchService;

    public UserSkillSubjectProvider(IUserSearchService userSearchService)
    {
        _userSearchService = userSearchService;
    }

    public string Kind => SkillSubjectKind.User;

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var wanted = (ids ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (wanted.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var users = await _userSearchService.GetAsync(wanted, cancellationToken);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var user in users.Where(user => !string.IsNullOrEmpty(user?.UserId)))
        {
            names[user.UserId] = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
        }

        return names;
    }

    public Task<bool> CanManageAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrEmpty(id));
}
