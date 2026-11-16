using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.ContentManagement;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// The members of a course as the services see them: its staff (with their roles) and its learners (one membership per
/// person, active while an enrollment is active or completed). It also finds the person behind the id a tool knows,
/// among the members of the course only.
/// </summary>
public sealed class LtiCourseMembers
{
    private const int EnrollmentPageSize = 500;

    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;
    private readonly UserManager<IUser> _userManager;
    private readonly IEnumerable<IDisplayNameProvider> _displayNameProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiCourseMembers"/> class.
    /// </summary>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="enrollments">The enrollment store.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="displayNameProviders">The display name providers.</param>
    public LtiCourseMembers(
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollments,
        UserManager<IUser> userManager,
        IEnumerable<IDisplayNameProvider> displayNameProviders)
    {
        _courseStructure = courseStructure;
        _enrollments = enrollments;
        _userManager = userManager;
        _displayNameProviders = displayNameProviders;
    }

    /// <summary>
    /// Lists the members of a published course, staff first, then learners in enrollment order.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The members; empty when the course is not published.</returns>
    public async Task<IReadOnlyList<LtiCourseMember>> ListAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var course = await _courseStructure.GetCourseAsync(courseId);

        if (course is null)
        {
            return [];
        }

        var members = new List<LtiCourseMember>();
        var byUser = new Dictionary<string, (LtiCourseMember Member, List<string> StaffRoles, bool Learner)>(StringComparer.Ordinal);

        foreach (var group in (course.Get<CoursePart>(nameof(CoursePart))?.Staff ?? []).Where(member => !string.IsNullOrEmpty(member.UserId)).GroupBy(member => member.UserId, StringComparer.Ordinal))
        {
            var member = new LtiCourseMember { UserId = group.Key, Active = true };

            byUser[group.Key] = (member, group.Select(staff => staff.Role).ToList(), false);
            members.Add(member);
        }

        for (var page = 1; ; page++)
        {
            var (count, items) = await _enrollments.PageAsync(new EnrollmentQuery { CourseId = course.ContentItemId }, page, EnrollmentPageSize, cancellationToken);

            foreach (var enrollment in items.Where(enrollment => enrollment.CourseId == course.ContentItemId && !string.IsNullOrEmpty(enrollment.UserId)))
            {
                var active = enrollment.Status is EnrollmentStatus.Active or EnrollmentStatus.Completed;

                if (byUser.TryGetValue(enrollment.UserId, out var entry))
                {
                    entry.Member.Active |= active;
                    byUser[enrollment.UserId] = (entry.Member, entry.StaffRoles, true);
                }
                else
                {
                    var member = new LtiCourseMember { UserId = enrollment.UserId, Active = active };

                    byUser[enrollment.UserId] = (member, [], true);
                    members.Add(member);
                }
            }

            if (page * EnrollmentPageSize >= count || items.Count == 0)
            {
                break;
            }
        }

        foreach (var (_, (member, staffRoles, learner)) in byUser)
        {
            member.Roles = LtiRoles.For(learner, staffRoles, courseManager: false);
        }

        return members;
    }

    /// <summary>
    /// Reads the names and the e-mail address of members.
    /// </summary>
    /// <param name="members">The members.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task FillNamesAsync(IEnumerable<LtiCourseMember> members, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(members);

        foreach (var member in members)
        {
            if (await _userManager.FindByIdAsync(member.UserId) is not { } user)
            {
                continue;
            }

            foreach (var provider in _displayNameProviders)
            {
                member.Name = await provider.GetAsync(user, cancellationToken);

                if (!string.IsNullOrWhiteSpace(member.Name))
                {
                    break;
                }
            }

            member.Name = string.IsNullOrWhiteSpace(member.Name) ? user.UserName : member.Name;

            if (user is User { Properties: { } properties } fullUser)
            {
                member.Email = fullUser.Email;

                if (properties["UserFullNamePart"] is JsonObject fullName)
                {
                    member.GivenName = fullName["FirstName"]?.GetValue<string>();
                    member.FamilyName = fullName["LastName"]?.GetValue<string>();
                }
            }
        }
    }

    /// <summary>
    /// Finds the member of a course a tool knows by an id.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="subject">The id the tool knows the person by.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The user id, or <see langword="null"/> when no member of the course has that id for the tool.</returns>
    public async Task<string> FindUserIdAsync(ExternalTool tool, string courseId, string subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (string.IsNullOrEmpty(subject))
        {
            return null;
        }

        foreach (var member in await ListAsync(courseId, cancellationToken))
        {
            if (string.Equals(LtiSubjects.For(tool, member.UserId), subject, StringComparison.Ordinal))
            {
                return member.UserId;
            }
        }

        return null;
    }
}
