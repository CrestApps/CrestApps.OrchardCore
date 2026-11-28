namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// Why one assessment item was not imported. The detail, when there is one, names what was refused.
/// </summary>
public static class QtiRefusals
{
    /// <summary>The item file is not well-formed XML, or it has a DTD or an entity.</summary>
    public const string InvalidXml = "InvalidXml";

    /// <summary>The manifest names an item file the package does not have.</summary>
    public const string MissingFile = "MissingFile";

    /// <summary>The file is not a QTI 2.1 or QTI 3.0 assessment item.</summary>
    public const string NotAnItem = "NotAnItem";

    /// <summary>The item uses an interaction no question type can hold; the detail names it.</summary>
    public const string UnsupportedInteraction = "UnsupportedInteraction";

    /// <summary>The item is adaptive: it changes as the learner answers.</summary>
    public const string Adaptive = "Adaptive";

    /// <summary>The item is a template whose values are drawn when it is shown.</summary>
    public const string Template = "Template";

    /// <summary>The item has no interaction.</summary>
    public const string NoInteraction = "NoInteraction";

    /// <summary>The item has interactions of several kinds, or several block interactions.</summary>
    public const string SeveralInteractions = "SeveralInteractions";

    /// <summary>The item has no correct response to score by.</summary>
    public const string NoCorrectResponse = "NoCorrectResponse";

    /// <summary>The response processing is not a standard template and not one this import recognizes.</summary>
    public const string CustomScoring = "CustomScoring";

    /// <summary>
    /// The item's scoring cannot be kept by the question type (mapped values that differ, penalties, a single choice with
    /// several right answers); the detail names it.
    /// </summary>
    public const string UnsupportedScoring = "UnsupportedScoring";

    /// <summary>A choice, an answer or a blank holds markup (an image, math, a table) where the question type keeps plain text; the detail names it.</summary>
    public const string MarkupInText = "MarkupInText";

    /// <summary>An accepted answer holds a character the question type cannot keep; the detail names it.</summary>
    public const string UnsupportedAnswer = "UnsupportedAnswer";

    /// <summary>The item refers to a media file the package does not have; the detail names it.</summary>
    public const string MissingMedia = "MissingMedia";

    /// <summary>The item refers to a media file of a kind that is not imported (only PNG, JPEG, GIF and WebP images are); the detail names it.</summary>
    public const string UnsupportedMedia = "UnsupportedMedia";

    /// <summary>The item refers to a media file larger than allowed; the detail names it.</summary>
    public const string MediaTooLarge = "MediaTooLarge";

    /// <summary>The item holds content that is not imported, such as math, audio, video or an object; the detail names it.</summary>
    public const string UnsupportedContent = "UnsupportedContent";

    /// <summary>The structure of the interaction cannot be kept by the question type, such as a matching distractor; the detail says what.</summary>
    public const string UnsupportedStructure = "UnsupportedStructure";
}
