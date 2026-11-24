namespace CrestApps.OrchardCore.Learning.Assessments.Core.Qti;

/// <summary>
/// The protected file area of the pictures that only the general feedback of imported QTI items shows. They stay out of
/// the media library, where anyone (or, with Secure Media, every signed-in user) could read them before the feedback is
/// shown, and are served by <see cref="RoutePath"/> to the people <see cref="Services.QtiFeedbackImageAccess"/> allows.
/// </summary>
public sealed class QtiFeedbackImages
{
    /// <summary>
    /// The folder of the area under the tenant's App_Data.
    /// </summary>
    public const string Folder = "LearningQtiFeedbackImages";

    /// <summary>
    /// The path of the endpoint that serves a picture, followed by its file id.
    /// </summary>
    public const string RoutePath = "learning/assessments/feedback-images";

    /// <summary>
    /// Returns the path of a picture's address, without the tenant's prefix.
    /// </summary>
    /// <param name="fileId">The protected file id.</param>
    /// <returns>The path.</returns>
    public static string PathOf(string fileId)
        => RoutePath + "/" + fileId;
}
