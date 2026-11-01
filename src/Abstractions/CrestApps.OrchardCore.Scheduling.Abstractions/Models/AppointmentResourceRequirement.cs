namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// A group of resources an appointment needs, such as one person from the staff and one room. An appointment type with several
/// requirements is only offered when every requirement can be met at the same time, and a booking takes a resource from each.
/// </summary>
public sealed class AppointmentResourceRequirement
{
    /// <summary>
    /// Gets or sets the group's name, such as Staff or Room, for the admin screens.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the resources in the group, in order of preference.
    /// </summary>
    public IList<string> ResourceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how the group's resources are picked: any one free resource, one shared out by round robin, or all of them.
    /// </summary>
    public AppointmentAssignment Assignment { get; set; }

    /// <summary>
    /// Gets or sets the round-robin weight of some of the group's resources, by resource id. A resource with weight 2 is given
    /// about twice as many appointments as one with weight 1, the weight of a resource that isn't listed.
    /// </summary>
    public IDictionary<string, int> Weights { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the round-robin weight of a resource in the group, at least 1.
    /// </summary>
    /// <param name="resourceId">The resource.</param>
    /// <returns>The weight.</returns>
    public int GetWeight(string resourceId)
    {
        return resourceId is not null && Weights is not null && Weights.TryGetValue(resourceId, out var weight) && weight > 1 ? weight : 1;
    }
}
