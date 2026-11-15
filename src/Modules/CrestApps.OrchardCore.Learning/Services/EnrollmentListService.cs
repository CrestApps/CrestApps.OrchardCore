using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Works out which enrollments a person may list: every enrollment with the View enrollments permission, otherwise
/// the enrollments of the courses where the person is on the course staff.
/// </summary>
public sealed class EnrollmentListService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly IUserSearchService _userSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentListService"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="userSearchService">The user search service, which resolves display names.</param>
    public EnrollmentListService(
        IAuthorizationService authorizationService,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollmentStore,
        IUserSearchService userSearchService)
    {
        _authorizationService = authorizationService;
        _courseStructure = courseStructure;
        _enrollmentStore = enrollmentStore;
        _userSearchService = userSearchService;
    }

    /// <summary>
    /// Narrows a query to what a person may see.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="false"/> when the person may see no enrollment at all.</returns>
    public async Task<bool> ApplyScopeAsync(ClaimsPrincipal user, EnrollmentQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await _authorizationService.AuthorizeAsync(user, LearningPermissions.ViewEnrollments))
        {
            return true;
        }

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var courseIds = await _courseStructure.GetStaffCourseIdsAsync(userId);

        query.CourseIds = courseIds;

        return courseIds.Count > 0;
    }

    /// <summary>
    /// Gets the courses a person may enroll learners into: <see langword="null"/> for every course (the person holds
    /// Manage enrollments), else the courses where they are an instructor or assistant and hold Manage own courses.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <returns>The course ids, or <see langword="null"/> for every course.</returns>
    public async Task<IReadOnlyCollection<string>> GetEnrollableCourseIdsAsync(ClaimsPrincipal user)
    {
        if (await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageEnrollments))
        {
            return null;
        }

        if (!await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses))
        {
            return [];
        }

        return await _courseStructure.GetStaffCourseIdsAsync(user?.FindFirstValue(ClaimTypes.NameIdentifier), LearningConstants.CourseStaffRoles.Instructor, LearningConstants.CourseStaffRoles.Assistant);
    }

    /// <summary>
    /// Gets whether a user exists.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public async Task<bool> UserExistsAsync(string userId)
        => !string.IsNullOrWhiteSpace(userId) && (await _userSearchService.GetAsync([userId])).Any(user => user.UserId == userId);

    /// <summary>
    /// Pages enrollments within a person's scope.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="query">The query.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The count and the page, or <see langword="null"/> when the person may see no enrollment.</returns>
    public async Task<(int Count, IReadOnlyList<Models.Enrollment> Items)?> PageAsync(ClaimsPrincipal user, EnrollmentQuery query, int page, int pageSize)
    {
        if (!await ApplyScopeAsync(user, query))
        {
            return null;
        }

        return await _enrollmentStore.PageAsync(query, page, pageSize);
    }

    /// <summary>
    /// Resolves the names of learners.
    /// </summary>
    /// <param name="userIds">The user ids.</param>
    /// <returns>The names by user id.</returns>
    public async Task<IDictionary<string, string>> GetNamesAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var names = ids.ToDictionary(id => id, id => id, StringComparer.Ordinal);

        if (ids.Length == 0)
        {
            return names;
        }

        foreach (var user in await _userSearchService.GetAsync(ids))
        {
            names[user.UserId] = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
        }

        return names;
    }
}
