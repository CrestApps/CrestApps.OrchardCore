namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Represents the processing state of a local DNC list import.
/// </summary>
public enum LocalDncListStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Paused,
    Deleting,
}
