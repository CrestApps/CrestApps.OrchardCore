using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Tells who may approve the completion of a course that waits for the staff: anyone who manages the enrollment, or,
/// when the course names a role, only its staff members in that role.
/// </summary>
public static class CompletionApprovers
{
    /// <summary>
    /// Gets the role the course names for approvals, or <see langword="null"/> when it names none.
    /// </summary>
    /// <param name="part">The course.</param>
    /// <returns>The role.</returns>
    public static string RoleOf(CoursePart part)
        => part?.CompletionCriteria?
            .Where(rule => rule is not null && string.Equals(rule.Name, LearningRuleNames.StaffApproval, StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule.Get("Role")?.Trim())
            .FirstOrDefault(role => !string.IsNullOrEmpty(role));

    /// <summary>
    /// Tells whether a person may approve the completion.
    /// </summary>
    /// <param name="part">The course.</param>
    /// <param name="userId">The person.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    public static bool MayApprove(CoursePart part, string userId)
    {
        var role = RoleOf(part);

        if (role is null)
        {
            return true;
        }

        return !string.IsNullOrEmpty(userId) && (part.Staff ?? []).Any(staff =>
            staff is not null &&
            string.Equals(staff.UserId, userId, StringComparison.Ordinal) &&
            string.Equals(staff.Role, role, StringComparison.OrdinalIgnoreCase));
    }
}
