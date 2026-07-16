namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Resolves the effective owner assignment type for current and legacy subject action metadata.
/// </summary>
public static class SubjectActionOwnerAssignmentTypeResolver
{
    /// <summary>
    /// Resolves the effective assignment type, treating a legacy configured username as a specific owner.
    /// </summary>
    /// <param name="assignmentType">The explicitly configured assignment type.</param>
    /// <param name="normalizedUserName">The configured normalized username, if any.</param>
    public static SubjectActionOwnerAssignmentType Resolve(
        SubjectActionOwnerAssignmentType assignmentType,
        string normalizedUserName)
    {
        if (assignmentType == SubjectActionOwnerAssignmentType.SpecificOwner ||
            !string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return SubjectActionOwnerAssignmentType.SpecificOwner;
        }

        return SubjectActionOwnerAssignmentType.SameOwner;
    }
}
