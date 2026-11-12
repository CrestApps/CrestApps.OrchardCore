using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// The default <see cref="IStaffDirectory"/>. It caches what it reads for the request, because lists ask for the same
/// people many times.
/// </summary>
internal sealed class StaffDirectory : IStaffDirectory
{
    private readonly StaffMemberStore _store;
    private readonly IClock _clock;
    private readonly StaffOptions _options;
    private readonly Dictionary<string, StaffMember> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StaffMember> _byUserId = new(StringComparer.Ordinal);

    public StaffDirectory(
        StaffMemberStore store,
        IClock clock,
        IOptions<StaffOptions> options)
    {
        _store = store;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<bool> IsStaffAsync(string userId, CancellationToken cancellationToken = default)
    {
        var staff = await FindByUserIdAsync(userId, cancellationToken);

        return staff is not null && staff.IsEmployedOn(DateOnly.FromDateTime(_clock.UtcNow));
    }

    public async Task<StaffMember> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        if (!_byUserId.TryGetValue(userId, out var staff))
        {
            staff = await _store.FindByUserIdAsync(userId, cancellationToken);
            _byUserId[userId] = staff;

            if (staff is not null)
            {
                _byId[staff.ItemId] = staff;
            }
        }

        return staff;
    }

    public async Task<IReadOnlyDictionary<string, StaffMember>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var wanted = (ids ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var missing = wanted.Where(id => !_byId.ContainsKey(id)).ToArray();

        if (missing.Length > 0)
        {
            foreach (var staff in await _store.GetAsync(missing, cancellationToken))
            {
                _byId[staff.ItemId] = staff;

                if (!string.IsNullOrEmpty(staff.UserId))
                {
                    _byUserId[staff.UserId] = staff;
                }
            }
        }

        var found = new Dictionary<string, StaffMember>(StringComparer.Ordinal);

        foreach (var id in wanted)
        {
            if (_byId.TryGetValue(id, out var staff) && staff is not null)
            {
                found[id] = staff;
            }
        }

        return found;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var staff = await GetAsync(ids, cancellationToken);

        return staff.ToDictionary(pair => pair.Key, pair => pair.Value.DisplayName, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesByUserIdAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var wanted = (userIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var missing = wanted.Where(id => !_byUserId.ContainsKey(id)).ToArray();

        if (missing.Length > 0)
        {
            var found = await _store.GetByUserIdsAsync(missing, cancellationToken);

            foreach (var id in missing)
            {
                _byUserId[id] = null;
            }

            foreach (var staff in found)
            {
                _byUserId[staff.UserId] = staff;
                _byId[staff.ItemId] = staff;
            }
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var id in wanted)
        {
            if (_byUserId.TryGetValue(id, out var staff) && staff is not null)
            {
                names[id] = staff.DisplayName;
            }
        }

        return names;
    }

    public async Task<IReadOnlyList<StaffMember>> SearchAsync(string text, string capability = null, bool requireUser = false, int take = 20, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var query = new StaffQuery
        {
            Search = text,
            Capability = string.IsNullOrWhiteSpace(capability) ? null : capability,
            HasUser = requireUser ? true : null,
        };

        // Ask for more than needed: people on leave count, terminated ones don't.
        var (_, items) = await _store.SearchAsync(query, null, 0, Math.Clamp(take, 1, 100) * 2, cancellationToken);

        return items.Where(staff => staff.IsEmployedOn(today)).Take(Math.Clamp(take, 1, 100)).ToArray();
    }

    public IReadOnlyList<StaffCapability> GetCapabilities()
        => _options.Capabilities.Values.OrderBy(capability => capability.Position).ThenBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase).ToArray();
}
