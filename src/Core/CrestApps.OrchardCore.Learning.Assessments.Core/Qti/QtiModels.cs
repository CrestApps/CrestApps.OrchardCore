namespace CrestApps.OrchardCore.Learning.Assessments.Core.Qti;

/// <summary>
/// Why a whole file could not be read as QTI.
/// </summary>
public static class QtiErrors
{
    /// <summary>The file is neither a zip archive nor an XML file.</summary>
    public const string NotQti = "NotQti";

    /// <summary>The archive was refused by the safe extraction rules; the detail names the rule and the entry.</summary>
    public const string UnsafeArchive = "UnsafeArchive";

    /// <summary>The archive has no <c>imsmanifest.xml</c> at its root.</summary>
    public const string NoManifest = "NoManifest";

    /// <summary>The manifest is not well-formed XML, or it has a DTD.</summary>
    public const string InvalidManifest = "InvalidManifest";

    /// <summary>The manifest lists no QTI 2.1 or QTI 3.0 assessment item.</summary>
    public const string NoItems = "NoItems";

    /// <summary>The package has more items than allowed.</summary>
    public const string TooManyItems = "TooManyItems";
}

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

/// <summary>
/// A QTI file read: its items, or why the whole file was refused.
/// </summary>
public sealed class QtiReadResult
{
    /// <summary>Gets or sets the error, one of <see cref="QtiErrors"/>, or <see langword="null"/>.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets what the error names, such as the archive rule and entry.</summary>
    public string ErrorDetail { get; set; }

    /// <summary>Gets or sets the items, in the order of the manifest.</summary>
    public IReadOnlyList<QtiItem> Items { get; set; } = [];

    /// <summary>Gets whether the file was read.</summary>
    public bool Succeeded => Error is null;
}

/// <summary>
/// One assessment item: the question it became, or why it was refused.
/// </summary>
public sealed class QtiItem
{
    /// <summary>Gets or sets the identifier of the item.</summary>
    public string Identifier { get; set; }

    /// <summary>Gets or sets the title of the item.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the path of the item file in the package.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets the QTI version: <c>2.1</c> (2.x) or <c>3.0</c>.</summary>
    public string Version { get; set; }

    /// <summary>Gets or sets the question, or <see langword="null"/> when the item was refused.</summary>
    public QtiQuestion Question { get; set; }

    /// <summary>Gets or sets why the item was refused, one of <see cref="QtiRefusals"/>, or <see langword="null"/>.</summary>
    public string Refusal { get; set; }

    /// <summary>Gets or sets what the refusal names.</summary>
    public string RefusalDetail { get; set; }

    /// <summary>Gets or sets the media files the prompt and feedback refer to.</summary>
    public IReadOnlyList<QtiMediaFile> Media { get; set; } = [];

    /// <summary>Gets whether the item became a question.</summary>
    public bool Imported => Refusal is null && Question is not null;
}

/// <summary>
/// A media file of a package that an item's HTML refers to. The HTML refers to it as
/// <see cref="QtiMediaFile.Reference"/> until the import stores it and writes its address.
/// </summary>
public sealed class QtiMediaFile
{
    /// <summary>
    /// The scheme of a reference to a media file in the HTML of an imported question.
    /// </summary>
    public const string Scheme = "qti-media:";

    /// <summary>
    /// The extensions of the images an import takes: the media a package may refer to.
    /// </summary>
    public static readonly IReadOnlySet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
    };

    /// <summary>
    /// Tells whether bytes are an image of one of the <see cref="ImageExtensions"/>, by their signature: PNG, JPEG, GIF
    /// or WebP.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <returns><see langword="true"/> when they start like an image of those types.</returns>
    public static bool HasImageSignature(ReadOnlySpan<byte> content)
        => content.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        || content.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])
        || content.StartsWith("GIF87a"u8)
        || content.StartsWith("GIF89a"u8)
        || (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8));

    /// <summary>Gets or sets the path of the file in the package, normalized.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets the bytes of the file.</summary>
    public byte[] Content { get; set; }

    /// <summary>Gets the text that refers to the file in the HTML of the question.</summary>
    public string Reference => Scheme + Path;
}

/// <summary>
/// A question read from an assessment item, in the terms of the question types.
/// </summary>
public sealed class QtiQuestion
{
    /// <summary>Gets or sets the question type, one of <see cref="AssessmentsConstants.QuestionTypes"/>.</summary>
    public string QuestionType { get; set; }

    /// <summary>Gets or sets the prompt, as HTML.</summary>
    public string Prompt { get; set; }

    /// <summary>Gets or sets the points, from the outcome declarations and the response processing.</summary>
    public decimal Points { get; set; } = 1;

    /// <summary>Gets or sets the feedback shown whatever the answer, as HTML.</summary>
    public string GeneralFeedback { get; set; }

    /// <summary>Gets or sets the options of a choice question, or the items of an ordering question in their right order.</summary>
    public IList<QtiOption> Options { get; set; } = [];

    /// <summary>Gets or sets whether the options are shuffled.</summary>
    public bool Shuffle { get; set; }

    /// <summary>Gets or sets whether a partly right answer earns part of the points.</summary>
    public bool PartialCredit { get; set; }

    /// <summary>Gets or sets the answer of a true or false question.</summary>
    public bool TrueFalseAnswer { get; set; }

    /// <summary>Gets or sets the accepted answers of a short text question.</summary>
    public IList<string> AcceptedAnswers { get; set; } = [];

    /// <summary>Gets or sets whether letter case matters.</summary>
    public bool CaseSensitive { get; set; }

    /// <summary>Gets or sets the answer of a numeric question.</summary>
    public decimal NumericAnswer { get; set; }

    /// <summary>Gets or sets the tolerance of a numeric question.</summary>
    public decimal Tolerance { get; set; }

    /// <summary>Gets or sets the pairs of a matching question.</summary>
    public IList<(string Prompt, string Answer)> Pairs { get; set; } = [];

    /// <summary>Gets or sets the text of a fill in the blanks question, each blank written as <c>[[answer|other answer]]</c>.</summary>
    public string BlankText { get; set; }

    /// <summary>Gets or sets the guidance graders see, from a rubric block for scorers.</summary>
    public string GraderGuidance { get; set; }
}

/// <summary>
/// An option of a choice question, or an item of an ordering question.
/// </summary>
public sealed class QtiOption
{
    /// <summary>Gets or sets the identifier of the choice in the item.</summary>
    public string Identifier { get; set; }

    /// <summary>Gets or sets the text.</summary>
    public string Text { get; set; }

    /// <summary>Gets or sets whether the option is right.</summary>
    public bool Correct { get; set; }

    /// <summary>Gets or sets the feedback shown when the option was chosen, as plain text.</summary>
    public string Feedback { get; set; }
}
