using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Skills.Core;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Skills.Reports;

/// <summary>
/// Who holds which skill today: per skill the number of holders and their average level, and one row per person and
/// skill. Learning shows skills as competencies, with the category as the competency framework, so the report can be
/// narrowed to one category. Skills given in the chosen period are counted too.
/// </summary>
internal sealed class SkillHoldersReport : IReport, IReportFilterMetadata
{
    /// <summary>
    /// The most rows of people the report lists; the counts per skill are always complete up to this many assignments.
    /// </summary>
    internal const int MaxAssignments = 5000;

    private readonly ISkillManager _skillManager;
    private readonly SkillAssignmentStore _assignments;
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public SkillHoldersReport(
        ISkillManager skillManager,
        SkillAssignmentStore assignments,
        ISession session,
        IDisplayNameProvider displayNameProvider,
        ILocalClock localClock,
        IStringLocalizer<SkillHoldersReport> stringLocalizer)
    {
        _skillManager = skillManager;
        _assignments = assignments;
        _session = session;
        _displayNameProvider = displayNameProvider;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public static string Name => "skills-skill-holders";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SkillHoldersReport>("Skill holders");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SkillHoldersReport>("Who holds each skill or competency today, at what level and since when, for one category (competency framework) or all of them.");

    public static string Category => ReportsConstants.Categories.Operations;

    public static Permission Permission => SkillsPermissions.ViewSkills;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public IReadOnlyCollection<string> FilterNames => [SkillsReportFilters.Category];

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var category = context.Filter.GetOrDefault<string>(SkillsReportFilters.CategoryKey);
        var skills = (await _skillManager.GetAllAsync())
            .Where(skill => skill.Enabled)
            .Where(skill => string.IsNullOrEmpty(category) || string.Equals(skill.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(skill => skill.ItemId, StringComparer.Ordinal);
        var assignments = await _assignments.ListEffectiveAsync(skills.Keys, SkillsDates.ToKey(today), MaxAssignments, cancellationToken);
        var names = await GetNamesAsync(assignments, cancellationToken);
        var range = context.Filter.GetDateRange();
        var givenInPeriod = assignments.Count(assignment => (range.FromUtc is null || assignment.AssignedUtc >= range.FromUtc) && (range.ToUtc is null || assignment.AssignedUtc < range.ToUtc));

        var perSkill = skills.Values
            .Select(skill => (Skill: skill, Holders: assignments.Where(assignment => assignment.SkillId == skill.ItemId).ToList()))
            .OrderBy(item => item.Skill.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Skill.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new ReportRow(
            [
                item.Skill.Category ?? string.Empty,
                item.Skill.Name,
                ReportFormat.Number(item.Holders.Count),
                item.Holders.Count == 0 ? string.Empty : item.Holders.Average(assignment => assignment.Proficiency).ToString("0.0", CultureInfo.CurrentCulture),
            ]))
            .ToList();

        var people = assignments
            .Select(assignment => (Assignment: assignment, Skill: skills[assignment.SkillId], Person: names.GetValueOrDefault(assignment.Subject.Key) ?? assignment.SubjectId))
            .OrderBy(item => item.Skill.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Skill.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Person, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new ReportRow(
            [
                item.Person,
                item.Skill.Name,
                item.Skill.Category ?? string.Empty,
                ReportFormat.Number(item.Assignment.Proficiency),
                item.Assignment.EffectiveFrom?.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty,
                item.Assignment.EffectiveTo?.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty,
                item.Assignment.AssignedBy ?? string.Empty,
            ]))
            .ToList();

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Today"].Value,
            [
                new ReportMetric(S["People holding a skill"].Value, ReportFormat.Number(assignments.Select(assignment => assignment.Subject.Key).Distinct(StringComparer.Ordinal).Count())),
                new ReportMetric(S["Skills held"].Value, ReportFormat.Number(assignments.Count)),
                new ReportMetric(S["Skills with no holder"].Value, ReportFormat.Number(skills.Count - assignments.Select(assignment => assignment.SkillId).Distinct(StringComparer.Ordinal).Count())),
                new ReportMetric(S["Given or changed in the period"].Value, ReportFormat.Number(givenInPeriod)),
            ]))
            .Add(ReportSection.ForTable(S["Skills"].Value,
            [
                new ReportColumn(S["Category"].Value),
                new ReportColumn(S["Skill"].Value),
                new ReportColumn(S["Holders"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average level"].Value, ReportColumnAlign.End),
            ], perSkill))
            .Add(ReportSection.ForTable(S["Holders"].Value,
            [
                new ReportColumn(S["Person"].Value),
                new ReportColumn(S["Skill"].Value),
                new ReportColumn(S["Category"].Value),
                new ReportColumn(S["Level"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Since"].Value),
                new ReportColumn(S["Until"].Value),
                new ReportColumn(S["Given by"].Value),
            ], people));
    }

    private async Task<Dictionary<string, string>> GetNamesAsync(IReadOnlyList<SkillAssignment> assignments, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var userIds = assignments
            .Where(assignment => assignment.SubjectKind == SkillSubjectKind.User)
            .Select(assignment => assignment.SubjectId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var chunk in userIds.Chunk(500))
        {
            foreach (var user in await _session.Query<User, UserIndex>(index => index.UserId.IsIn(chunk)).ListAsync(cancellationToken))
            {
                var displayName = await _displayNameProvider.GetAsync(user, cancellationToken);

                names[SkillSubject.User(user.UserId).Key] = string.IsNullOrWhiteSpace(displayName) ? user.UserName : displayName;
            }
        }

        return names;
    }
}

/// <summary>
/// The names of the Skills report filters, and the keys their values are stored under.
/// </summary>
public static class SkillsReportFilters
{
    /// <summary>
    /// The filter that narrows a report to one skill category (a competency framework).
    /// </summary>
    public const string Category = "SkillCategory";

    /// <summary>
    /// The key of the chosen category.
    /// </summary>
    public const string CategoryKey = "SkillCategory";
}
