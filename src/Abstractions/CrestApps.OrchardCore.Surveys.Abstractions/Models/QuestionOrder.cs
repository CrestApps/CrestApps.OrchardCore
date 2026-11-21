namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The order questions or pages show in.
/// </summary>
public enum QuestionOrder
{
    /// <summary>
    /// In the designed order.
    /// </summary>
    AsDefined,

    /// <summary>
    /// In a random order; pinned items keep their place.
    /// </summary>
    Shuffle,
}
