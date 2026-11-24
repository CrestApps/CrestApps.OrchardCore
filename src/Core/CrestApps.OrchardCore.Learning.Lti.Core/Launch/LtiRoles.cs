namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Maps a person's place in a course to LIS context roles: an enrollment is <c>Learner</c>; course staff are
/// <c>Instructor</c> (an assistant is also <c>TeachingAssistant</c>, a grader also <c>Grader</c>, both sub-roles of
/// Instructor sent with their principal role) and an observer is <c>Mentor</c>; a person who manages the course without
/// being on its staff is <c>Administrator</c> and <c>Instructor</c>.
/// </summary>
public static class LtiRoles
{
    /// <summary>
    /// Returns the context roles.
    /// </summary>
    /// <param name="learner">Whether the person is enrolled.</param>
    /// <param name="staffRoles">The person's roles on the course staff, such as <c>Instructor</c>.</param>
    /// <param name="courseManager">Whether the person manages the course without being on its staff.</param>
    /// <returns>The role URIs, without duplicates.</returns>
    public static IReadOnlyList<string> For(bool learner, IEnumerable<string> staffRoles, bool courseManager)
    {
        var roles = new List<string>();

        void Add(string role)
        {
            if (!roles.Contains(role, StringComparer.Ordinal))
            {
                roles.Add(role);
            }
        }

        foreach (var staffRole in staffRoles ?? [])
        {
            switch (staffRole)
            {
                case LearningConstants.CourseStaffRoles.Instructor:
                    Add(LtiConstants.Roles.Instructor);
                    break;

                case LearningConstants.CourseStaffRoles.Assistant:
                    Add(LtiConstants.Roles.Instructor);
                    Add(LtiConstants.Roles.TeachingAssistant);
                    break;

                case LearningConstants.CourseStaffRoles.Grader:
                    Add(LtiConstants.Roles.Instructor);
                    Add(LtiConstants.Roles.Grader);
                    break;

                case LearningConstants.CourseStaffRoles.Observer:
                    Add(LtiConstants.Roles.Mentor);
                    break;

                default:
                    Add(LtiConstants.Roles.Instructor);
                    break;
            }
        }

        if (courseManager && roles.Count == 0)
        {
            Add(LtiConstants.Roles.ContextAdministrator);
            Add(LtiConstants.Roles.Instructor);
        }

        if (learner)
        {
            Add(LtiConstants.Roles.Learner);
        }

        return roles;
    }
}
