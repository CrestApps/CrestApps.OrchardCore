namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The names of the built-in question types, for code that creates questions (templates, importers, AI tools). Logic
/// never switches on these names: everything a type does goes through its handler.
/// </summary>
public static class SurveyControlNames
{
    /// <summary>
    /// One choice from a list.
    /// </summary>
    public const string SingleChoice = "SingleChoice";

    /// <summary>
    /// Several choices from a list.
    /// </summary>
    public const string MultipleChoice = "MultipleChoice";

    /// <summary>
    /// Yes or no, true or false, or a consent box.
    /// </summary>
    public const string Boolean = "Boolean";

    /// <summary>
    /// A short text.
    /// </summary>
    public const string ShortText = "ShortText";

    /// <summary>
    /// A long text or essay.
    /// </summary>
    public const string LongText = "LongText";

    /// <summary>
    /// A number.
    /// </summary>
    public const string Number = "Number";

    /// <summary>
    /// A date, a time or both.
    /// </summary>
    public const string Date = "Date";

    /// <summary>
    /// A rating on a scale.
    /// </summary>
    public const string Rating = "Rating";

    /// <summary>
    /// Net Promoter Score: a 0 to 10 rating.
    /// </summary>
    public const string Nps = "Nps";

    /// <summary>
    /// Customer satisfaction: a 1 to 5 rating.
    /// </summary>
    public const string Csat = "Csat";

    /// <summary>
    /// A grid of rows and columns.
    /// </summary>
    public const string Matrix = "Matrix";

    /// <summary>
    /// Items put in order.
    /// </summary>
    public const string Ranking = "Ranking";

    /// <summary>
    /// Prompts matched to answers.
    /// </summary>
    public const string Matching = "Matching";

    /// <summary>
    /// A text with blanks to type into.
    /// </summary>
    public const string FillInBlanks = "FillInBlanks";

    /// <summary>
    /// A text with blanks to fill from given words.
    /// </summary>
    public const string ChoiceBlanks = "ChoiceBlanks";

    /// <summary>
    /// Text and media that need no answer.
    /// </summary>
    public const string TextBlock = "TextBlock";

    /// <summary>
    /// File uploads.
    /// </summary>
    public const string FileUpload = "FileUpload";

    /// <summary>
    /// A hidden value from a variable.
    /// </summary>
    public const string Hidden = "Hidden";

    /// <summary>
    /// A value calculated by a formula.
    /// </summary>
    public const string Calculated = "Calculated";

    /// <summary>
    /// Name, e-mail, phone, company and address.
    /// </summary>
    public const string ContactInfo = "ContactInfo";

    /// <summary>
    /// A number picked on a slider.
    /// </summary>
    public const string Slider = "Slider";

    /// <summary>
    /// Numbers that must add up to a total.
    /// </summary>
    public const string ConstantSum = "ConstantSum";

    /// <summary>
    /// A drawn signature.
    /// </summary>
    public const string Signature = "Signature";

    /// <summary>
    /// Several short texts.
    /// </summary>
    public const string MultipleText = "MultipleText";

    /// <summary>
    /// A grid with a drop-down in each cell.
    /// </summary>
    public const string MatrixDropdown = "MatrixDropdown";
}
