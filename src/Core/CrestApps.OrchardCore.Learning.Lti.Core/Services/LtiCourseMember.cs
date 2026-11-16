namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// A member of a course as the services see it: a learner (from their enrollment) or a member of the course staff, with
/// their LIS roles and, for the roster, their names and e-mail address.
/// </summary>
public sealed class LtiCourseMember
{
    /// <summary>
    /// Gets or sets the user id. It never leaves the platform.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets whether the membership is active: an active or completed enrollment, or the course staff.
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Gets or sets the LIS context roles.
    /// </summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the full name, when read.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the given name, when read.
    /// </summary>
    public string GivenName { get; set; }

    /// <summary>
    /// Gets or sets the family name, when read.
    /// </summary>
    public string FamilyName { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address, when read.
    /// </summary>
    public string Email { get; set; }
}
