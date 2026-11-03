namespace CrestApps.OrchardCore.Scheduling.ViewModels;

/// <summary>
/// One group of resources in the appointment type editor.
/// </summary>
public class AppointmentResourceRequirementViewModel
{
    /// <summary>Gets or sets the group's name, such as Staff or Room.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the resources in the group.</summary>
    public string[] ResourceIds { get; set; } = [];

    /// <summary>Gets or sets how the group's resources are picked.</summary>
    public string Assignment { get; set; }

    /// <summary>Gets or sets the round-robin weight of each resource, by resource id.</summary>
    public Dictionary<string, int> Weights { get; set; } = [];
}
