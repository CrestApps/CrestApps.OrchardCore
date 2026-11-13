using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// The access rules of an enrollment: access stays while any grant remains, until the latest end of its grants.
/// </summary>
public static class EnrollmentAccess
{
    /// <summary>
    /// Computes when access ends from the grants.
    /// </summary>
    /// <param name="grants">The grants.</param>
    /// <returns>The latest end, or <see langword="null"/> when a grant has no end or there is no grant.</returns>
    public static DateTime? ComputeAccessEnds(IEnumerable<EnrollmentGrant> grants)
    {
        DateTime? latest = null;

        foreach (var grant in grants ?? [])
        {
            if (grant.AccessEndsUtc is null)
            {
                return null;
            }

            if (latest is null || grant.AccessEndsUtc > latest)
            {
                latest = grant.AccessEndsUtc;
            }
        }

        return latest;
    }

    /// <summary>
    /// Tells whether an enrollment gives access now: it is active, has a grant and its access has not ended.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when the learner may work on the course.</returns>
    public static bool HasAccess(Enrollment enrollment, DateTime utcNow)
    {
        if (enrollment is null || enrollment.Status != EnrollmentStatus.Active || enrollment.Grants is null || enrollment.Grants.Count == 0)
        {
            return false;
        }

        return enrollment.AccessEndsUtc is null || enrollment.AccessEndsUtc > utcNow;
    }
}
