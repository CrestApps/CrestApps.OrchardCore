using CrestApps.Core;
using System.Security.Cryptography;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Core.Indexes;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Security;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// The default <see cref="IStaffBadgeService"/>.
/// </summary>
internal sealed class StaffBadgeService : IStaffBadgeService
{
    private const int MaxFailedPins = 5;
    private const int PinIterations = 100_000;
    private static readonly TimeSpan _lockout = TimeSpan.FromMinutes(15);

    private readonly StaffMemberStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public StaffBadgeService(
        StaffMemberStore store,
        IClock clock,
        IStringLocalizer<StaffBadgeService> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public string SetPin(StaffMember staffMember, string pin)
    {
        ArgumentNullException.ThrowIfNull(staffMember);

        if (string.IsNullOrWhiteSpace(pin))
        {
            ClearPin(staffMember);

            return null;
        }

        pin = pin.Trim();

        if (pin.Length is < 4 or > 8 || !pin.All(char.IsAsciiDigit))
        {
            return S["A PIN is 4 to 8 digits."];
        }

        if (string.IsNullOrEmpty(staffMember.EmployeeNumber))
        {
            return S["Give {0} an employee number first: it is their badge number.", staffMember.DisplayName];
        }

        var salt = RandomNumberGenerator.GetBytes(16);

        staffMember.Put(new StaffBadge
        {
            PinSalt = Convert.ToBase64String(salt),
            PinHash = HashPin(pin, salt),
        });

        return null;
    }

    public void ClearPin(StaffMember staffMember)
    {
        ArgumentNullException.ThrowIfNull(staffMember);

        staffMember.Put(new StaffBadge());
    }

    public bool HasPin(StaffMember staffMember)
        => staffMember is not null && staffMember.TryGet<StaffBadge>(out var badge) && !string.IsNullOrEmpty(badge.PinHash);

    public async Task<StaffBadgeResult> VerifyAsync(string employeeNumber, string pin, CancellationToken cancellationToken = default)
    {
        var staff = string.IsNullOrWhiteSpace(employeeNumber) ? null : await _store.FindByEmployeeNumberAsync(employeeNumber.Trim(), cancellationToken);

        if (staff is null || !staff.TryGet<StaffBadge>(out var badge) || string.IsNullOrEmpty(badge.PinHash))
        {
            return new StaffBadgeResult { Failure = StaffBadgeFailure.Invalid };
        }

        var now = _clock.UtcNow;

        if (badge.LockedUntilUtc > now)
        {
            return new StaffBadgeResult { Failure = StaffBadgeFailure.Locked, LockedUntilUtc = badge.LockedUntilUtc, StaffMember = staff };
        }

        var matches = !string.IsNullOrEmpty(pin)
            && CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(HashPin(pin.Trim(), Convert.FromBase64String(badge.PinSalt))),
                Convert.FromBase64String(badge.PinHash));

        if (!matches)
        {
            badge.FailedAttempts++;

            if (badge.FailedAttempts >= MaxFailedPins)
            {
                badge.FailedAttempts = 0;
                badge.LockedUntilUtc = now + _lockout;
            }

            staff.Put(badge);
            await _store.UpdateAsync(staff, cancellationToken);

            return new StaffBadgeResult { Failure = StaffBadgeFailure.Invalid };
        }

        if (badge.FailedAttempts > 0 || badge.LockedUntilUtc.HasValue)
        {
            badge.FailedAttempts = 0;
            badge.LockedUntilUtc = null;
            staff.Put(badge);
            await _store.UpdateAsync(staff, cancellationToken);
        }

        if (!staff.IsEmployedOn(DateOnly.FromDateTime(now)))
        {
            return new StaffBadgeResult { Failure = StaffBadgeFailure.Inactive, StaffMember = staff };
        }

        return new StaffBadgeResult { Succeeded = true, StaffMember = staff };
    }

    internal static string HashPin(string pin, byte[] salt)
        => Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(pin, salt, PinIterations, HashAlgorithmName.SHA256, 32));
}

/// <summary>
/// Gives Teams the members Staff keeps, people without a user account included.
/// </summary>
internal sealed class StaffTeamMemberProvider : ITeamMemberProvider
{
    private readonly ISession _session;

    public StaffTeamMemberProvider(ISession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<TeamMember>> GetMembersAsync(IReadOnlyCollection<string> teamIds, CancellationToken cancellationToken = default)
    {
        if (teamIds is null || teamIds.Count == 0)
        {
            return [];
        }

        var ids = teamIds.ToArray();
        var memberships = await _session.QueryIndex<StaffTeamMembershipIndex>(index => index.TeamId.IsIn(ids), collection: StaffConstants.CollectionName)
            .ListAsync(cancellationToken);

        return await ToMembersAsync(memberships, cancellationToken);
    }

    public async Task<IReadOnlyList<TeamMember>> GetMembershipsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var staff = await _session.QueryIndex<StaffMemberIndex>(index => index.UserId == userId, collection: StaffConstants.CollectionName)
            .ListAsync(cancellationToken);
        var staffIds = staff.Select(row => row.ItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (staffIds.Length == 0)
        {
            return [];
        }

        var memberships = await _session.QueryIndex<StaffTeamMembershipIndex>(index => index.StaffMemberId.IsIn(staffIds), collection: StaffConstants.CollectionName)
            .ListAsync(cancellationToken);

        return await ToMembersAsync(memberships, cancellationToken);
    }

    private async Task<IReadOnlyList<TeamMember>> ToMembersAsync(IEnumerable<StaffTeamMembershipIndex> memberships, CancellationToken cancellationToken)
    {
        var rows = memberships.Where(membership => !string.IsNullOrEmpty(membership.StaffMemberId) && !string.IsNullOrEmpty(membership.TeamId)).ToArray();

        if (rows.Length == 0)
        {
            return [];
        }

        var staffIds = rows.Select(row => row.StaffMemberId).Distinct(StringComparer.Ordinal).ToArray();
        var people = await _session.QueryIndex<StaffMemberIndex>(index => index.ItemId.IsIn(staffIds), collection: StaffConstants.CollectionName)
            .ListAsync(cancellationToken);
        var userIds = people
            .GroupBy(row => row.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().UserId, StringComparer.Ordinal);

        return [.. rows
            .Where(row => userIds.ContainsKey(row.StaffMemberId))
            .Select(row => new TeamMember
            {
                TeamId = row.TeamId,
                UserId = userIds[row.StaffMemberId],
                StaffMemberId = row.StaffMemberId,
                Role = row.Role.ToString(),
                EffectiveFrom = StaffKeys.FromKey(row.EffectiveFrom),
                EffectiveTo = StaffKeys.FromKey(row.EffectiveTo),
                Source = StaffConstants.TeamMemberSource,
            })];
    }
}

/// <summary>
/// Links a staff member and their user as one skill holder, so skills given to either count for both.
/// </summary>
public sealed class StaffSkillSubjectLinkProvider : ISkillSubjectLinkProvider
{
    private readonly StaffMemberStore _store;

    // Per request: coverage and matching ask about the same people many times.
    private readonly Dictionary<SkillSubject, IReadOnlyList<SkillSubject>> _links = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffSkillSubjectLinkProvider"/> class.
    /// </summary>
    /// <param name="store">The staff store.</param>
    public StaffSkillSubjectLinkProvider(StaffMemberStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillSubject>> GetLinkedSubjectsAsync(SkillSubject subject, CancellationToken cancellationToken = default)
    {
        if (subject.IsEmpty)
        {
            return [];
        }

        if (!_links.TryGetValue(subject, out var links))
        {
            links = await FindLinksAsync(subject, cancellationToken);
            _links[subject] = links;
        }

        return links;
    }

    private async Task<IReadOnlyList<SkillSubject>> FindLinksAsync(SkillSubject subject, CancellationToken cancellationToken)
    {
        if (subject.Kind == SkillSubjectKind.Staff)
        {
            var staff = await _store.FindByIdAsync(subject.Id, cancellationToken);

            return string.IsNullOrEmpty(staff?.UserId) ? [] : [SkillSubject.User(staff.UserId)];
        }

        if (subject.Kind == SkillSubjectKind.User)
        {
            var staff = await _store.FindByUserIdAsync(subject.Id, cancellationToken);

            return staff is null ? [] : [SkillSubject.Staff(staff.ItemId)];
        }

        return [];
    }
}

/// <summary>
/// Makes staff members a party kind, so a record can belong to or be assigned to a person who works for the business.
/// </summary>
internal sealed class StaffPartySource : IPartySource
{
    private readonly IStaffDirectory _directory;

    public StaffPartySource(IStaffDirectory directory)
    {
        _directory = directory;
    }

    public string Kind => StaffConstants.PartyKind;

    public async Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var people = await _directory.SearchAsync(query, take: count, cancellationToken: cancellationToken);

        return [.. people.Select(ToOption)];
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        => await _directory.GetNamesAsync(ids ?? [], cancellationToken);

    private static PartyOption ToOption(StaffMember staff)
        => new(staff.ItemId, string.IsNullOrEmpty(staff.JobTitle) ? staff.DisplayName : $"{staff.DisplayName} ({staff.JobTitle})");
}

/// <summary>
/// Puts every working staff member with a user account in the Staff role, and takes them out when they stop working
/// or the account is unlinked, so role-filtered pickers and permission stereotypes can target staff.
/// </summary>
internal sealed class StaffRoleLifecycleObserver : IStaffLifecycleObserver
{
    private readonly StaffRoles _roles;

    public StaffRoleLifecycleObserver(StaffRoles roles)
    {
        _roles = roles;
    }

    public async Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
    {
        if (context.UserChanged && !string.IsNullOrEmpty(context.PreviousUserId))
        {
            await _roles.RevokeAsync(context.PreviousUserId, context.StaffMember.ItemId);
        }

        await _roles.SyncAsync(context.StaffMember, cancellationToken);
    }

    public Task DeletedAsync(StaffMember staffMember, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(staffMember.UserId) ? Task.CompletedTask : _roles.RevokeAsync(staffMember.UserId, staffMember.ItemId);
}

/// <summary>
/// Keeps the Staff role: creates it when it is missing, and puts working staff with an account in it.
/// </summary>
public sealed class StaffRoles
{
    private readonly IRoleGrantService _roleGrantService;
    private readonly RoleManager<IRole> _roleManager;
    private readonly IOptions<StaffRoleOptions> _options;
    private readonly IClock _clock;
    private bool _ensured;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffRoles"/> class.
    /// </summary>
    /// <param name="roleGrantService">The role grant service.</param>
    /// <param name="roleManager">The role manager.</param>
    /// <param name="options">The role options.</param>
    /// <param name="clock">The clock.</param>
    public StaffRoles(
        IRoleGrantService roleGrantService,
        RoleManager<IRole> roleManager,
        IOptions<StaffRoleOptions> options,
        IClock clock)
    {
        _roleGrantService = roleGrantService;
        _roleManager = roleManager;
        _options = options;
        _clock = clock;
    }

    /// <summary>
    /// Gets the role name, or <see langword="null"/> when Staff puts nobody in a role.
    /// </summary>
    public string RoleName => string.IsNullOrWhiteSpace(_options.Value.RoleName) ? null : _options.Value.RoleName.Trim();

    /// <summary>
    /// Creates the Staff role when it is missing.
    /// </summary>
    /// <returns><see langword="true"/> when the role exists afterwards.</returns>
    public async Task<bool> EnsureRoleAsync()
    {
        if (RoleName is not { } roleName)
        {
            return false;
        }

        if (_ensured || await _roleManager.FindByNameAsync(_roleManager.NormalizeKey(roleName)) is not null)
        {
            return _ensured = true;
        }

        var result = await _roleManager.CreateAsync(new Role
        {
            RoleName = roleName,
            RoleDescription = "Everyone who works for the business. Staff puts working staff members with a user account in this role and takes them out when they leave. Give it only the permissions every member of staff needs.",
        });

        return _ensured = result.Succeeded;
    }

    /// <summary>
    /// Puts a staff member's user in the role when they are working, and takes them out when not.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task SyncAsync(StaffMember staff, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staff);

        if (RoleName is not { } roleName || string.IsNullOrEmpty(staff.UserId))
        {
            return;
        }

        var granted = (await _roleGrantService.ListAsync(staff.UserId))
            .Any(grant => string.Equals(grant.RoleName, roleName, StringComparison.OrdinalIgnoreCase)
                && grant.Source == StaffConstants.RoleGrantSource
                && grant.SourceId == staff.ItemId);
        var working = staff.IsEmployedOn(DateOnly.FromDateTime(_clock.UtcNow));

        if (working && !granted && await EnsureRoleAsync())
        {
            await _roleGrantService.GrantAsync(staff.UserId, roleName, StaffConstants.RoleGrantSource, staff.ItemId);
        }
        else if (!working && granted)
        {
            await _roleGrantService.RevokeAsync(staff.UserId, roleName, StaffConstants.RoleGrantSource, staff.ItemId);
        }
    }

    /// <summary>
    /// Takes a user out of the role on behalf of a staff member.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="staffMemberId">The staff member.</param>
    /// <returns>A task.</returns>
    public Task RevokeAsync(string userId, string staffMemberId)
        => RoleName is { } roleName ? _roleGrantService.RevokeAsync(userId, roleName, StaffConstants.RoleGrantSource, staffMemberId) : Task.CompletedTask;
}

/// <summary>
/// The role Staff puts working staff members in.
/// </summary>
public sealed class StaffRoleOptions
{
    /// <summary>
    /// Gets or sets the role name; empty to put nobody in a role.
    /// </summary>
    public string RoleName { get; set; } = StaffConstants.DefaultStaffRoleName;
}

/// <summary>
/// Defines the <see cref="StaffConstants.UserAudience"/> user audience: the members of the Staff role.
/// </summary>
internal sealed class StaffUserAudienceProvider : IUserAudienceProvider
{
    private readonly IOptions<StaffRoleOptions> _options;

    public StaffUserAudienceProvider(IOptions<StaffRoleOptions> options)
    {
        _options = options;
    }

    public string Name => StaffConstants.UserAudience;

    public Task<IReadOnlyCollection<string>> GetRoleNamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>(string.IsNullOrWhiteSpace(_options.Value.RoleName) ? [] : [_options.Value.RoleName]);
}
