namespace CrestApps.OrchardCore.Staff;

/// <summary>
/// The staff profile page: the sections down its side, which modules fill with display drivers on
/// <c>StaffMember</c> for the <see cref="DisplayType"/> display type, placed with <see cref="Location"/>. A module that
/// adds a section of its own registers its title, icon and position with
/// <c>services.AddPageSection(StaffProfileSections.PageName, ...)</c>.
/// </summary>
public static class StaffProfileSections
{
    /// <summary>
    /// The page name the profile's sections are registered for.
    /// </summary>
    public const string PageName = "Staff";

    /// <summary>
    /// The display type of the profile page.
    /// </summary>
    public const string DisplayType = "DetailAdmin";

    /// <summary>
    /// The person, their employment and their reporting line. Shapes placed without a section land here.
    /// </summary>
    public const string Overview = "Overview";

    /// <summary>
    /// The teams the person is in (from the Teams module).
    /// </summary>
    public const string Teams = "Teams";

    /// <summary>
    /// Their skills and certificates (from the Skills module).
    /// </summary>
    public const string Skills = "Skills";

    /// <summary>
    /// Their shifts, time clock, timesheets and time off (from Workforce).
    /// </summary>
    public const string Work = "Work";

    /// <summary>
    /// Their pay (from Staff Compensation, later from Payroll).
    /// </summary>
    public const string Pay = "Pay";

    /// <summary>
    /// The changes to their employment, newest first.
    /// </summary>
    public const string History = "History";

    /// <summary>
    /// Gets the placement of a shape in a section of the profile.
    /// </summary>
    /// <param name="section">The section name.</param>
    /// <param name="position">The shape's position inside the section.</param>
    /// <returns>The placement, such as <c>Content:2#Teams</c>.</returns>
    public static string Location(string section, string position = "1")
        => $"Content:{position}#{section}";
}
