using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Works out the resource requirements one search or booking must meet.
/// </summary>
internal static class SchedulingRequirements
{
    /// <summary>
    /// Gets the requirements of a type for a request. A resource the customer chose narrows its own requirement to just that
    /// resource and leaves the others as they are.
    /// </summary>
    /// <param name="appointmentType">The appointment type.</param>
    /// <param name="resourceId">The chosen resource, or <see langword="null"/>.</param>
    /// <returns>The requirements, or <see langword="null"/> when the chosen resource isn't one of the type's.</returns>
    public static List<AppointmentResourceRequirement> ForRequest(AppointmentType appointmentType, string resourceId)
    {
        var requirements = appointmentType.GetResourceRequirements();

        if (resourceId is null)
        {
            return requirements.ToList();
        }

        if (!requirements.Any(requirement => requirement.ResourceIds.Contains(resourceId)))
        {
            return null;
        }

        return requirements
            .Select(requirement => requirement.ResourceIds.Contains(resourceId)
                ? new AppointmentResourceRequirement
                {
                    Name = requirement.Name,
                    ResourceIds = [resourceId],
                    Assignment = requirement.Assignment,
                    Weights = requirement.Weights,
                }
                : requirement)
            .ToList();
    }
}
