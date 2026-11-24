using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// An audience rule that matches the active staff members linked to a user who were hired at least some days ago.
/// </summary>
public sealed class StaffHireDateAudienceRuleSource : IUserAudienceRuleSource
{
    /// <summary>
    /// The name of the source.
    /// </summary>
    public const string SourceName = "StaffHireDate";

    private readonly IStaffMemberManager _staff;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffHireDateAudienceRuleSource"/> class.
    /// </summary>
    public StaffHireDateAudienceRuleSource(IStaffMemberManager staff, ILocalClock localClock, IStringLocalizer<StaffHireDateAudienceRuleSource> stringLocalizer)
    {
        _staff = staff;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => SourceName;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Hired at least some days ago"];

    /// <inheritdoc/>
    public LocalizedString NumberLabel => S["Days since hire"];

    /// <inheritdoc/>
    public Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserAudienceRuleOption>>([]);

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
    {
        var latest = await LatestHireDateAsync(rule);

        // Hire dates are not indexed, so they are compared on the active staff linked to a user.
        return (await StaffAudienceRules.ActiveLinkedAsync(_staff, null, cancellationToken))
            .Where(member => member.HireDate is { } hired && hired <= latest)
            .Select(member => member.UserId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<bool> IsMatchAsync(UserAudienceRule rule, string userId, CancellationToken cancellationToken = default)
    {
        var member = await _staff.FindByUserIdAsync(userId, cancellationToken);

        return member is { Status: StaffStatus.Active, HireDate: { } hired } && hired <= await LatestHireDateAsync(rule);
    }

    private async Task<DateOnly> LatestHireDateAsync(UserAudienceRule rule)
    {
        // The day is the site's: someone hired on the 1st has worked 30 days on the 31st there, whatever the UTC time.
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        return today.AddDays(-Math.Max(0, rule.Number ?? 0));
    }
}

/// <summary>
/// An audience rule that matches the active staff members linked to a user with some employment types.
/// </summary>
public sealed class StaffEmploymentTypeAudienceRuleSource : IUserAudienceRuleSource
{
    /// <summary>
    /// The name of the source.
    /// </summary>
    public const string SourceName = "StaffEmploymentType";

    private readonly IStaffMemberManager _staff;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffEmploymentTypeAudienceRuleSource"/> class.
    /// </summary>
    public StaffEmploymentTypeAudienceRuleSource(IStaffMemberManager staff, IStringLocalizer<StaffEmploymentTypeAudienceRuleSource> stringLocalizer)
    {
        _staff = staff;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => SourceName;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Employment type"];

    /// <inheritdoc/>
    public Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserAudienceRuleOption>>(
        [
            new(nameof(EmploymentType.FullTime), S["Full time"]),
            new(nameof(EmploymentType.PartTime), S["Part time"]),
            new(nameof(EmploymentType.Seasonal), S["Seasonal"]),
            new(nameof(EmploymentType.Temporary), S["Temporary"]),
            new(nameof(EmploymentType.Contractor), S["Contractor"]),
            new(nameof(EmploymentType.Subcontractor), S["Subcontractor"]),
        ]);

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
    {
        var types = rule.Values
            .Select(value => Enum.TryParse<EmploymentType>(value, out var type) ? type : (EmploymentType?)null)
            .Where(type => type is not null)
            .Select(type => type.Value)
            .ToArray();

        if (types.Length == 0)
        {
            return [];
        }

        return (await StaffAudienceRules.ActiveLinkedAsync(_staff, types, cancellationToken))
            .Select(member => member.UserId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<bool> IsMatchAsync(UserAudienceRule rule, string userId, CancellationToken cancellationToken = default)
    {
        var member = await _staff.FindByUserIdAsync(userId, cancellationToken);

        return member is { Status: StaffStatus.Active } && rule.Values.Contains(member.EmploymentType.ToString());
    }
}

internal static class StaffAudienceRules
{
    private const int PageSize = 500;

    public static async Task<IReadOnlyList<StaffMember>> ActiveLinkedAsync(IStaffMemberManager staff, IReadOnlyCollection<EmploymentType> employmentTypes, CancellationToken cancellationToken)
    {
        var members = new List<StaffMember>();
        var query = new StaffQuery { Status = StaffStatus.Active, HasUser = true, EmploymentTypes = employmentTypes };

        for (var skip = 0; ; skip += PageSize)
        {
            var (count, items) = await staff.SearchAsync(query, skip, PageSize, cancellationToken);

            members.AddRange(items.Where(member => !string.IsNullOrEmpty(member.UserId)));

            if (items.Count < PageSize || skip + PageSize >= count)
            {
                return members;
            }
        }
    }
}
