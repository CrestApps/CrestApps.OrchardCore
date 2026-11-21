namespace CrestApps.OrchardCore.Learning.Core.Moodle;

/// <summary>
/// Matches the cohort of a Moodle cohort sync enrolment with a cohort of the site: a site cohort whose id is the Moodle
/// cohort's id number, else the Moodle cohort's id, else one whose name (ignoring case) is the Moodle cohort's name,
/// else the enrolment's own name. A backup keeps the cohort's Moodle id in <c>customint1</c>; its id number and name
/// only when the backup carries them.
/// </summary>
public static class MoodleCohorts
{
    /// <summary>
    /// Finds the site cohort of a cohort sync enrolment.
    /// </summary>
    /// <param name="enrolment">The enrolment.</param>
    /// <param name="cohorts">The site's cohorts, by id and name.</param>
    /// <returns>The id of the matching cohort, or <see langword="null"/>.</returns>
    public static string Match(MoodleEnrolment enrolment, IEnumerable<(string Id, string Name)> cohorts)
    {
        if (enrolment is null || cohorts is null)
        {
            return null;
        }

        var list = cohorts.ToList();

        foreach (var id in new[] { enrolment.CohortIdNumber, enrolment.CohortId }.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (list.FirstOrDefault(cohort => string.Equals(cohort.Id, id.Trim(), StringComparison.Ordinal)) is { Id: not null } byId)
            {
                return byId.Id;
            }
        }

        foreach (var name in new[] { enrolment.CohortName, enrolment.Name }.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (list.FirstOrDefault(cohort => string.Equals(cohort.Name?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)) is { Id: not null } byName)
            {
                return byName.Id;
            }
        }

        return null;
    }
}
