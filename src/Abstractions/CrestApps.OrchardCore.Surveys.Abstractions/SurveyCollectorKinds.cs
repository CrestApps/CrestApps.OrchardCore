namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The built-in kinds of collector. A kind is an open string: modules that start responses for their own records use
/// their own kind.
/// </summary>
public static class SurveyCollectorKinds
{
    /// <summary>
    /// A public link.
    /// </summary>
    public const string WebLink = "WebLink";

    /// <summary>
    /// Personal invitations.
    /// </summary>
    public const string Invitation = "Invitation";

    /// <summary>
    /// A survey embedded in a page.
    /// </summary>
    public const string Embed = "Embed";

    /// <summary>
    /// A kiosk that returns to the start after each response.
    /// </summary>
    public const string Kiosk = "Kiosk";
}
