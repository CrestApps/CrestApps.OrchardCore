using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// The kinds of learner lists the staff tools return.
/// </summary>
public enum LearnerRecordsListKind
{
    /// <summary>
    /// Active enrollments whose due date has passed.
    /// </summary>
    Overdue,

    /// <summary>
    /// Active enrollments whose access ends within a window.
    /// </summary>
    ExpiringSoon,
}

/// <summary>
/// One learner of a staff list: status and dates only, never answers or grades.
/// </summary>
public sealed class LearnerRecordsRow
{
    /// <summary>
    /// Gets or sets the learner's name.
    /// </summary>
    public string Learner { get; set; }

    /// <summary>
    /// Gets or sets the course title.
    /// </summary>
    public string Course { get; set; }

    /// <summary>
    /// Gets or sets the enrollment number.
    /// </summary>
    public string Enrollment { get; set; }

    /// <summary>
    /// Gets or sets the local due date and time.
    /// </summary>
    public string Due { get; set; }

    /// <summary>
    /// Gets or sets the local time access ends.
    /// </summary>
    public string AccessEnds { get; set; }

    /// <summary>
    /// Gets or sets the share of required items completed.
    /// </summary>
    public int ProgressPercent { get; set; }
}

/// <summary>
/// A staff list of learners.
/// </summary>
public sealed class LearnerRecordsList
{
    /// <summary>
    /// Gets or sets whether the person may list learners at all.
    /// </summary>
    public bool Allowed { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public IList<LearnerRecordsRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets whether more rows may exist than were looked at.
    /// </summary>
    public bool MayHaveMore { get; set; }

    /// <summary>
    /// Gets or sets the time zone the times are in.
    /// </summary>
    public string TimeZone { get; set; }
}

/// <summary>
/// Lists overdue learners and learners whose access ends soon, within the caller's scope: everyone for a person who
/// holds View learner records on its own; the learners of the courses a person is on the staff of, with Manage the
/// courses you teach (the Learning record authorization handler's rule); and, when Staff is on, the people a manager
/// manages (<see cref="IStaffScopeService"/>). Rows outside the scope are never returned.
/// </summary>
internal sealed class LearnerRecordsLister
{
    private const int PageSize = 100;
    private const int MaxPages = 10;

    private readonly IAuthorizationService _authorizationService;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;
    private readonly IUserSearchService _users;
    private readonly IServiceProvider _services;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnerRecordsLister"/> class.
    /// </summary>
    public LearnerRecordsLister(
        IAuthorizationService authorizationService,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollments,
        IUserSearchService users,
        IServiceProvider services,
        IClock clock,
        ILocalClock localClock)
    {
        _authorizationService = authorizationService;
        _courseStructure = courseStructure;
        _enrollments = enrollments;
        _users = users;
        _services = services;
        _clock = clock;
        _localClock = localClock;
    }

    /// <summary>
    /// Lists learners.
    /// </summary>
    /// <param name="user">The person asking.</param>
    /// <param name="kind">The list.</param>
    /// <param name="days">For learners whose access ends soon, the window in days.</param>
    /// <param name="take">The most rows to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The list.</returns>
    public async Task<LearnerRecordsList> ListAsync(ClaimsPrincipal user, LearnerRecordsListKind kind, int days, int take, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return new LearnerRecordsList();
        }

        take = Math.Clamp(take, 1, 50);

        var everyone = await _authorizationService.AuthorizeAsync(user, LearningPermissions.ViewLearnerRecords);
        var courseIds = !everyone && await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses)
            ? new HashSet<string>(await _courseStructure.GetStaffCourseIdsAsync(userId), StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var staffScope = everyone ? StaffScope.None : await GetStaffScopeAsync(user, cancellationToken);

        if (!everyone && courseIds.Count == 0 && staffScope.IsEmpty)
        {
            return new LearnerRecordsList();
        }

        var query = CreateQuery(kind, days);
        var rows = new List<Enrollment>();
        var mayHaveMore = false;

        if (everyone || staffScope.IsEmpty)
        {
            if (!everyone)
            {
                query.CourseIds = courseIds;
            }

            var (count, items) = await _enrollments.PageAsync(query, 1, take, cancellationToken);
            rows.AddRange(items.Where(enrollment => !string.Equals(enrollment.UserId, everyone ? null : userId, StringComparison.Ordinal)));
            mayHaveMore = count > items.Count;
        }
        else
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow);
            var members = _services.GetService<IStaffMemberManager>();

            for (var page = 1; page <= MaxPages && rows.Count < take; page++)
            {
                var (count, items) = await _enrollments.PageAsync(query, page, PageSize, cancellationToken);
                var staff = members is null
                    ? new Dictionary<string, StaffMember>(StringComparer.Ordinal)
                    : (await members.GetByUserIdsAsync(items.Select(item => item.UserId).Distinct(StringComparer.Ordinal), cancellationToken))
                        .Where(member => !string.IsNullOrEmpty(member.UserId))
                        .GroupBy(member => member.UserId, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

                foreach (var enrollment in items)
                {
                    if (string.Equals(enrollment.UserId, userId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (courseIds.Contains(enrollment.CourseId ?? string.Empty) ||
                        (staff.TryGetValue(enrollment.UserId ?? string.Empty, out var member) && staffScope.Covers(member, today)))
                    {
                        rows.Add(enrollment);
                    }
                }

                if (page * PageSize >= count)
                {
                    break;
                }

                mayHaveMore = page == MaxPages;
            }

            if (rows.Count > take)
            {
                rows = rows.Take(take).ToList();
                mayHaveMore = true;
            }
        }

        return new LearnerRecordsList
        {
            Allowed = true,
            Rows = await DescribeAsync(rows),
            MayHaveMore = mayHaveMore,
            TimeZone = await LearningAIDates.TimeZoneAsync(_localClock),
        };
    }

    private EnrollmentQuery CreateQuery(LearnerRecordsListKind kind, int days)
    {
        var now = _clock.UtcNow;

        return kind == LearnerRecordsListKind.Overdue
            ? new EnrollmentQuery
            {
                Status = EnrollmentStatus.Active,
                DueBeforeUtc = now,
                Order = EnrollmentOrder.DueSoonest,
            }
            : new EnrollmentQuery
            {
                Status = EnrollmentStatus.Active,
                AccessEndsAfterUtc = now,
                AccessEndsBeforeUtc = now.AddDays(Math.Clamp(days, 1, 90)),
                Order = EnrollmentOrder.AccessEndsSoonest,
            };
    }

    private async Task<StaffScope> GetStaffScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var scopeService = _services.GetService<IStaffScopeService>();

        return scopeService is null ? StaffScope.None : await scopeService.GetScopeAsync(user, cancellationToken) ?? StaffScope.None;
    }

    private async Task<IList<LearnerRecordsRow>> DescribeAsync(IReadOnlyList<Enrollment> enrollments)
    {
        var names = (await _users.GetAsync(enrollments.Select(enrollment => enrollment.UserId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)))
            .ToDictionary(found => found.UserId, found => string.IsNullOrWhiteSpace(found.DisplayName) ? found.UserName : found.DisplayName, StringComparer.Ordinal);
        var courses = new Dictionary<string, string>(StringComparer.Ordinal);
        var rows = new List<LearnerRecordsRow>();

        foreach (var enrollment in enrollments)
        {
            if (!courses.TryGetValue(enrollment.CourseId ?? string.Empty, out var title))
            {
                title = (await _courseStructure.GetCourseAsync(enrollment.CourseId))?.DisplayText ?? enrollment.CourseId;
                courses[enrollment.CourseId ?? string.Empty] = title;
            }

            rows.Add(new LearnerRecordsRow
            {
                Learner = names.TryGetValue(enrollment.UserId ?? string.Empty, out var name) ? name : enrollment.UserId,
                Course = title,
                Enrollment = enrollment.Number,
                Due = await LearningAIDates.FormatAsync(_localClock, enrollment.DueUtc),
                AccessEnds = await LearningAIDates.FormatAsync(_localClock, enrollment.AccessEndsUtc),
                ProgressPercent = enrollment.ProgressPercent,
            });
        }

        return rows;
    }
}
