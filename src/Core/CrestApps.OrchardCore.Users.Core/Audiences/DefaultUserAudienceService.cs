using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Documents;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.Users.Core.Audiences;

/// <summary>
/// Resolves the members of the audiences modules register (the users in their roles, less the ones they leave out)
/// and of the audiences administrators define (their rules, joined by all or any).
/// </summary>
public sealed class DefaultUserAudienceService : IUserAudienceService
{
    private const int MaxMembers = 20_000;

    private readonly IDocumentManager<UserAudiencesDocument> _documents;
    private readonly IEnumerable<IUserAudienceProvider> _providers;
    private readonly IEnumerable<IUserAudienceRuleSource> _sources;
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;

    // The service lives as long as the request or the run: an audience is resolved once in it, however many pages are
    // read, and one person is checked once per audience.
    private readonly Dictionary<string, IReadOnlyCollection<string>> _resolved = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Name, string UserId), bool> _membership = [];

    // The definitions this service saved or deleted: the cached immutable document does not show them until the
    // session commits, so the reads that follow a change in the same request or run use this copy instead.
    private UserAudiencesDocument _changed;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserAudienceService"/> class.
    /// </summary>
    public DefaultUserAudienceService(
        IDocumentManager<UserAudiencesDocument> documents,
        IEnumerable<IUserAudienceProvider> providers,
        IEnumerable<IUserAudienceRuleSource> sources,
        UserManager<IUser> userManager,
        ISession session,
        IStringLocalizer<DefaultUserAudienceService> stringLocalizer)
    {
        _session = session;
        _documents = documents;
        _providers = providers;
        _sources = sources;
        _userManager = userManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserAudienceSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var document = await GetDocumentAsync();

        return _providers
            .Select(provider => new UserAudienceSummary(provider.Name, provider.Name, IsDefined: false))
            .Concat(document.Audiences.Select(definition => new UserAudienceSummary(definition.Name, string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Name : definition.DisplayName, IsDefined: true)))
            .OrderBy(audience => audience.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<UserAudienceDefinition> FindDefinitionAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return (await GetDocumentAsync()).Find(name.Trim());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SaveDefinitionAsync(UserAudienceDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();

        definition.Name = definition.Name?.Trim();

        if (string.IsNullOrEmpty(definition.Name) || !definition.Name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
        {
            errors.Add(S["The technical name is required and may hold only letters, digits, dots, dashes and underscores."]);
        }
        else if (_providers.Any(provider => string.Equals(provider.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(S["A module already defines an audience named '{0}'.", definition.Name]);
        }

        definition.Rules = definition.Rules.Where(rule => !string.IsNullOrEmpty(rule.Source)).ToList();

        if (definition.Rules.Count == 0)
        {
            errors.Add(S["Add at least one rule."]);
        }

        foreach (var rule in definition.Rules.Where(rule => _sources.All(source => source.Name != rule.Source)))
        {
            errors.Add(S["The rule '{0}' is not available.", rule.Source]);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var document = await _documents.GetOrCreateMutableAsync();

        document.Audiences.RemoveAll(audience => string.Equals(audience.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
        document.Audiences.Add(definition);
        await _documents.UpdateAsync(document);
        Changed(document);

        return errors;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteDefinitionAsync(string name, CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetOrCreateMutableAsync();

        if (string.IsNullOrWhiteSpace(name) || document.Audiences.RemoveAll(audience => string.Equals(audience.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return false;
        }

        await _documents.UpdateAsync(document);
        Changed(document);

        return true;
    }

    /// <inheritdoc/>
    public async Task<(int Count, IReadOnlyList<string> UserIds)> GetMembersAsync(string name, int skip, int take, CancellationToken cancellationToken = default)
    {
        var key = name?.Trim() ?? string.Empty;

        if (!_resolved.TryGetValue(key, out var resolved))
        {
            resolved = (await ResolveAsync(name, cancellationToken)).Order(StringComparer.Ordinal).ToArray();
            _resolved[key] = resolved;
        }

        var members = (string[])resolved;

        return (members.Length, members.Skip(Math.Max(0, skip)).Take(Math.Max(0, take)).ToArray());
    }

    /// <inheritdoc/>
    public async Task<bool> IsMemberAsync(string name, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var cacheKey = (name?.Trim() ?? string.Empty, userId);

        if (_membership.TryGetValue(cacheKey, out var known))
        {
            return known;
        }

        var member = await IsMemberCoreAsync(name, userId, cancellationToken);

        _membership[cacheKey] = member;

        return member;
    }

    private async Task<bool> IsMemberCoreAsync(string name, string userId, CancellationToken cancellationToken)
    {
        var provider = FindProvider(name);

        if (provider is not null)
        {
            var roles = await provider.GetRoleNamesAsync(cancellationToken);
            var user = await _userManager.FindByIdAsync(userId);

            if (user is null)
            {
                return false;
            }

            if (roles is not null && !await InAnyRoleAsync(user, roles))
            {
                return false;
            }

            return !(await provider.GetExcludedUserIdsAsync([userId], cancellationToken)).Contains(userId);
        }

        var definition = await FindDefinitionAsync(name, cancellationToken);

        if (definition is null)
        {
            return false;
        }

        foreach (var rule in definition.Rules)
        {
            var source = _sources.FirstOrDefault(candidate => candidate.Name == rule.Source);
            var matches = source is not null && await source.IsMatchAsync(rule, userId, cancellationToken);

            if (definition.Join == UserAudienceJoin.Any && matches)
            {
                return true;
            }

            if (definition.Join == UserAudienceJoin.All && !matches)
            {
                return false;
            }
        }

        return definition.Join == UserAudienceJoin.All;
    }

    private async Task<IReadOnlyCollection<string>> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        var provider = FindProvider(name);

        if (provider is not null)
        {
            var roles = await provider.GetRoleNamesAsync(cancellationToken);
            var ids = new HashSet<string>(StringComparer.Ordinal);

            // An audience that is not limited right now names every enabled user, as IsMemberAsync answers: the same
            // meaning for a layer rule and for required learning. The ids come from the user index alone.
            if (roles is null)
            {
                foreach (var row in await _session.QueryIndex<UserIndex>(index => index.IsEnabled).Take(MaxMembers).ListAsync(cancellationToken))
                {
                    ids.Add(row.UserId);
                }
            }

            foreach (var role in roles ?? [])
            {
                foreach (var user in await _userManager.GetUsersInRoleAsync(role))
                {
                    ids.Add(await _userManager.GetUserIdAsync(user));

                    if (ids.Count >= MaxMembers)
                    {
                        break;
                    }
                }
            }

            ids.ExceptWith(await provider.GetExcludedUserIdsAsync(ids, cancellationToken));

            return ids;
        }

        var definition = await FindDefinitionAsync(name, cancellationToken);

        if (definition is null)
        {
            return [];
        }

        HashSet<string> members = null;

        foreach (var rule in definition.Rules)
        {
            var source = _sources.FirstOrDefault(candidate => candidate.Name == rule.Source);
            var matched = source is null ? [] : await source.ResolveUserIdsAsync(rule, cancellationToken);

            if (members is null)
            {
                members = new HashSet<string>(matched, StringComparer.Ordinal);
            }
            else if (definition.Join == UserAudienceJoin.All)
            {
                members.IntersectWith(matched);
            }
            else
            {
                members.UnionWith(matched);
            }
        }

        return members ?? [];
    }

    private async Task<UserAudiencesDocument> GetDocumentAsync()
        => _changed ?? await _documents.GetOrCreateImmutableAsync();

    private void Changed(UserAudiencesDocument document)
    {
        _changed = document;

        // A changed definition changes who is in it: what was resolved before the change is dropped.
        _resolved.Clear();
        _membership.Clear();
    }

    private IUserAudienceProvider FindProvider(string name)
        => string.IsNullOrWhiteSpace(name) ? null : _providers.FirstOrDefault(provider => string.Equals(provider.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    private async Task<bool> InAnyRoleAsync(IUser user, IEnumerable<string> roles)
    {
        foreach (var role in roles)
        {
            if (await _userManager.IsInRoleAsync(user, role))
            {
                return true;
            }
        }

        return false;
    }
}
