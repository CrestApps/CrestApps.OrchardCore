using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// An audience rule that matches the people who hold some skills today, the users themselves or through the records
/// linked to them, such as their staff record.
/// </summary>
internal sealed class SkillHolderAudienceRuleSource : IUserAudienceRuleSource
{
    public const string SourceName = "SkillHolder";

    private readonly ISkillManager _skills;
    private readonly ISkillAssignmentService _assignments;
    private readonly IEnumerable<ISkillSubjectLinkProvider> _links;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public SkillHolderAudienceRuleSource(
        ISkillManager skills,
        ISkillAssignmentService assignments,
        IEnumerable<ISkillSubjectLinkProvider> links,
        ILocalClock localClock,
        IStringLocalizer<SkillHolderAudienceRuleSource> stringLocalizer)
    {
        _skills = skills;
        _assignments = assignments;
        _links = links;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public string Name => SourceName;

    public LocalizedString DisplayName => S["Holds a skill"];

    public async Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _skills.GetEnabledAsync(cancellationToken))
            .OrderBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(skill => new UserAudienceRuleOption(skill.ItemId, skill.Name))
            .ToArray();

    public async Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
    {
        var today = await SkillsAudienceRules.TodayAsync(_localClock);
        var subjects = new List<SkillSubject>();

        foreach (var skillId in rule.Values)
        {
            foreach (var kind in new[] { SkillSubjectKind.User, SkillSubjectKind.Staff })
            {
                subjects.AddRange((await _assignments.FindHoldersAsync(skillId, 0, today, kind, cancellationToken)).Select(holder => new SkillSubject(holder.SubjectKind, holder.SubjectId)));
            }
        }

        return await SkillsAudienceRules.ToUserIdsAsync(subjects, _links, cancellationToken);
    }
}

/// <summary>
/// An audience rule that matches the people who hold a valid record of some certifications.
/// </summary>
internal sealed class CertificationHolderAudienceRuleSource : IUserAudienceRuleSource
{
    public const string SourceName = "CertificationHolder";

    private const int PageSize = 500;

    private readonly ICertificationManager _certifications;
    private readonly ICertificationRecordService _records;
    private readonly IEnumerable<ISkillSubjectLinkProvider> _links;

    internal readonly IStringLocalizer S;

    public CertificationHolderAudienceRuleSource(
        ICertificationManager certifications,
        ICertificationRecordService records,
        IEnumerable<ISkillSubjectLinkProvider> links,
        IStringLocalizer<CertificationHolderAudienceRuleSource> stringLocalizer)
    {
        _certifications = certifications;
        _records = records;
        _links = links;
        S = stringLocalizer;
    }

    public string Name => SourceName;

    public LocalizedString DisplayName => S["Holds a valid certification"];

    public async Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _certifications.GetEnabledAsync(cancellationToken))
            .OrderBy(certification => certification.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(certification => new UserAudienceRuleOption(certification.ItemId, certification.Name))
            .ToArray();

    public async Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
    {
        var subjects = new List<SkillSubject>();

        foreach (var certificationId in rule.Values)
        {
            var query = new CertificationRecordQuery { CertificationId = certificationId, Status = CertificationStatus.Valid };

            for (var skip = 0; ; skip += PageSize)
            {
                var (count, items) = await _records.PageAsync(query, skip, PageSize, cancellationToken);

                subjects.AddRange(items.Select(record => new SkillSubject(record.SubjectKind, record.SubjectId)));

                if (items.Count < PageSize || skip + PageSize >= count)
                {
                    break;
                }
            }
        }

        return await SkillsAudienceRules.ToUserIdsAsync(subjects, _links, cancellationToken);
    }
}

internal static class SkillsAudienceRules
{
    public static async Task<DateOnly> TodayAsync(ILocalClock localClock)
        => DateOnly.FromDateTime((await localClock.GetLocalNowAsync()).DateTime);

    /// <summary>
    /// Turns people of any kind into user ids: a user is kept, any other kind is followed through its links.
    /// </summary>
    public static async Task<IReadOnlyCollection<string>> ToUserIdsAsync(IEnumerable<SkillSubject> subjects, IEnumerable<ISkillSubjectLinkProvider> links, CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var subject in subjects.Where(subject => !subject.IsEmpty).Distinct())
        {
            if (subject.Kind == SkillSubjectKind.User)
            {
                ids.Add(subject.Id);

                continue;
            }

            foreach (var link in links)
            {
                foreach (var linked in await link.GetLinkedSubjectsAsync(subject, cancellationToken))
                {
                    if (linked.Kind == SkillSubjectKind.User && !linked.IsEmpty)
                    {
                        ids.Add(linked.Id);
                    }
                }
            }
        }

        return ids;
    }
}
