using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;

/// <summary>
/// The columns the QTI reader adds to the question columns.
/// </summary>
public static class QtiColumns
{
    /// <summary>
    /// Why the item is not imported. The QTI import handler fails a row that has it.
    /// </summary>
    public const string Error = "QtiError";

    /// <summary>
    /// The media files the prompt and feedback refer to, as a JSON array of <c>{ "Path", "Data" }</c> with the bytes in
    /// Base64. The QTI import handler stores them in the media library and writes their addresses in the HTML.
    /// </summary>
    public const string Media = "QtiMedia";
}

/// <summary>
/// Writes the items of a QTI file as rows of the question columns.
/// </summary>
public static class QtiQuestionRows
{
    /// <summary>
    /// Gets the columns, in order.
    /// </summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        ContentTransferColumns.ContentType,
        "ContentItemId",
        "TitlePart_Title",
        "QuestionPart_Prompt",
        "QuestionPart_Points",
        "QuestionPart_GeneralFeedback",
        "QuestionPart_SourceIdentifier",
        "QuestionPart_SourceFile",
        "ChoiceQuestionPart_Options",
        "ChoiceQuestionPart_ShuffleOptions",
        "ChoiceQuestionPart_PartialCredit",
        "ChoiceQuestionPart_OptionFeedback",
        "TrueFalseQuestionPart_Answer",
        "ShortTextQuestionPart_AcceptedAnswers",
        "ShortTextQuestionPart_CaseSensitive",
        "NumericQuestionPart_Answer",
        "NumericQuestionPart_Tolerance",
        "MatchingQuestionPart_Pairs",
        "MatchingQuestionPart_PartialCredit",
        "OrderingQuestionPart_Items",
        "OrderingQuestionPart_PartialCredit",
        "FillInBlanksQuestionPart_Text",
        "FillInBlanksQuestionPart_CaseSensitive",
        "EssayQuestionPart_GraderGuidance",
        "ChoiceBlanksQuestionPart_Text",
        "ChoiceBlanksQuestionPart_Distractors",
        "ChoiceBlanksQuestionPart_ShuffleChoices",
        "SurveyQuestionPart_Options",
        "SurveyQuestionPart_Multiple",
        QtiColumns.Error,
        QtiColumns.Media,
    ];

    /// <summary>
    /// Writes the rows of a file read: one per item, or one row with the error when the whole file was refused.
    /// </summary>
    /// <param name="result">The file read.</param>
    /// <param name="questionTypes">The question types, which give each row its content type.</param>
    /// <param name="S">The localizer of the errors.</param>
    /// <returns>The rows, each with a value per column.</returns>
    public static IReadOnlyList<string[]> Write(QtiReadResult result, QuestionTypeOptions questionTypes, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(questionTypes);
        ArgumentNullException.ThrowIfNull(S);

        if (!result.Succeeded)
        {
            var row = new Row();
            row[QtiColumns.Error] = FileError(result, S);

            return [row.Values];
        }

        var rows = new List<string[]>();

        foreach (var item in result.Items)
        {
            var row = new Row();
            row["TitlePart_Title"] = item.Title ?? item.Identifier ?? item.Path;
            var contentType = item.Imported ? questionTypes.Types.FirstOrDefault(type => type.Name == item.Question.QuestionType)?.ContentType : null;

            if (!item.Imported || contentType is null)
            {
                var reason = item.Imported ? S["the question type {0} is not enabled", item.Question.QuestionType].Value : Reason(item, S);
                row[QtiColumns.Error] = S["Item {0} ({1}) was not imported: {2}.", item.Identifier ?? item.Path, item.Path, reason];
                rows.Add(row.Values);

                continue;
            }

            var question = item.Question;
            row[ContentTransferColumns.ContentType] = contentType;
            row["QuestionPart_Prompt"] = question.Prompt ?? question.QuestionType switch
            {
                AssessmentsConstants.QuestionTypes.FillInBlanks => $"<p>{System.Net.WebUtility.HtmlEncode(S["Fill in the blanks."])}</p>",
                AssessmentsConstants.QuestionTypes.GapSelect => $"<p>{System.Net.WebUtility.HtmlEncode(S["Choose the missing words."])}</p>",
                AssessmentsConstants.QuestionTypes.DragIntoText => $"<p>{System.Net.WebUtility.HtmlEncode(S["Drag the missing words into the text."])}</p>",
                _ => $"<p>{System.Net.WebUtility.HtmlEncode(item.Title ?? item.Identifier)}</p>",
            };
            row["QuestionPart_Points"] = question.Points.ToString(CultureInfo.InvariantCulture);
            row["QuestionPart_GeneralFeedback"] = question.GeneralFeedback;
            row["QuestionPart_SourceIdentifier"] = item.Identifier;
            row["QuestionPart_SourceFile"] = item.Path;

            switch (question.QuestionType)
            {
                case AssessmentsConstants.QuestionTypes.SingleChoice or AssessmentsConstants.QuestionTypes.MultipleChoice:
                    row["ChoiceQuestionPart_Options"] = QuestionCells.Join(question.Options.Select(option => (option.Correct ? QuestionCells.RightMark.ToString() : string.Empty) + QuestionCells.EscapeValue(option.Text)));
                    row["ChoiceQuestionPart_ShuffleOptions"] = YesNo(question.Shuffle);
                    row["ChoiceQuestionPart_PartialCredit"] = YesNo(question.PartialCredit);

                    if (question.Options.Any(option => !string.IsNullOrEmpty(option.Feedback)))
                    {
                        row["ChoiceQuestionPart_OptionFeedback"] = QuestionCells.Join(question.Options.Select(option => QuestionCells.EscapeValue(option.Feedback)));
                    }

                    break;

                case AssessmentsConstants.QuestionTypes.TrueFalse:
                    row["TrueFalseQuestionPart_Answer"] = question.TrueFalseAnswer ? "true" : "false";
                    break;

                case AssessmentsConstants.QuestionTypes.ShortText:
                    row["ShortTextQuestionPart_AcceptedAnswers"] = QuestionCells.Join(question.AcceptedAnswers.Select(answer => QuestionCells.EscapeValue(answer)));
                    row["ShortTextQuestionPart_CaseSensitive"] = YesNo(question.CaseSensitive);
                    break;

                case AssessmentsConstants.QuestionTypes.Numeric:
                    row["NumericQuestionPart_Answer"] = question.NumericAnswer.ToString(CultureInfo.InvariantCulture);
                    row["NumericQuestionPart_Tolerance"] = question.Tolerance.ToString(CultureInfo.InvariantCulture);
                    break;

                case AssessmentsConstants.QuestionTypes.Matching:
                    row["MatchingQuestionPart_Pairs"] = QuestionCells.Join(question.Pairs.Select(pair => QuestionCells.EscapeValue(pair.Prompt, pair: true) + QuestionCells.PairSeparator + QuestionCells.EscapeValue(pair.Answer, pair: true)));
                    row["MatchingQuestionPart_PartialCredit"] = YesNo(question.PartialCredit);
                    break;

                case AssessmentsConstants.QuestionTypes.Ordering:
                    row["OrderingQuestionPart_Items"] = QuestionCells.Join(question.Options.Select(option => QuestionCells.EscapeValue(option.Text)));
                    row["OrderingQuestionPart_PartialCredit"] = YesNo(question.PartialCredit);
                    break;

                case AssessmentsConstants.QuestionTypes.FillInBlanks:
                    row["FillInBlanksQuestionPart_Text"] = question.BlankText;
                    row["FillInBlanksQuestionPart_CaseSensitive"] = YesNo(question.CaseSensitive);
                    break;

                case AssessmentsConstants.QuestionTypes.Essay:
                    row["EssayQuestionPart_GraderGuidance"] = question.GraderGuidance;
                    break;

                case AssessmentsConstants.QuestionTypes.DragIntoText or AssessmentsConstants.QuestionTypes.GapSelect:
                    row["ChoiceBlanksQuestionPart_Text"] = question.BlankText;
                    row["ChoiceBlanksQuestionPart_Distractors"] = QuestionCells.Join(question.Distractors.Select(word => QuestionCells.EscapeValue(word)));
                    row["ChoiceBlanksQuestionPart_ShuffleChoices"] = YesNo(question.Shuffle);
                    break;

                case AssessmentsConstants.QuestionTypes.Survey:
                    row["SurveyQuestionPart_Options"] = QuestionCells.Join(question.Options.Select(option => QuestionCells.EscapeValue(option.Text)));
                    row["SurveyQuestionPart_Multiple"] = YesNo(question.Multiple);
                    break;
            }

            if (item.Media.Count > 0)
            {
                row[QtiColumns.Media] = JsonSerializer.Serialize(item.Media.Select(file => new QtiMediaCell { Path = file.Path, Data = Convert.ToBase64String(file.Content) }));
            }

            rows.Add(row.Values);
        }

        return rows;
    }

    private static string YesNo(bool value)
        => value ? "yes" : "no";

    private static string FileError(QtiReadResult result, IStringLocalizer S)
        => result.Error switch
        {
            QtiErrors.UnsafeArchive => S["The package was refused by the safe extraction rules: {0}.", result.ErrorDetail],
            QtiErrors.NoManifest => S["The .zip file has no imsmanifest.xml at its root, so it is not a QTI content package."],
            QtiErrors.InvalidManifest => S["The imsmanifest.xml file is not well-formed XML, or it has a DTD, which is refused."],
            QtiErrors.NoItems => S["The imsmanifest.xml file lists no QTI 2.1 or QTI 3.0 assessment item."],
            QtiErrors.TooManyItems => S["The package lists more than {0} items.", result.ErrorDetail],
            _ => S["The file is not a QTI 3.0 or QTI 2.1 content package or assessment item."],
        };

    private static string Reason(QtiItem item, IStringLocalizer S)
        => item.Refusal switch
        {
            QtiRefusals.InvalidXml => S["the file is not well-formed XML, or it has a DTD or an entity, which are refused"],
            QtiRefusals.MissingFile => S["the package has no file {0}", item.RefusalDetail],
            QtiRefusals.NotAnItem => S["it is not a QTI 2.1 or QTI 3.0 assessment item"],
            QtiRefusals.UnsupportedInteraction => S["it uses a {0}, which no question type can hold", item.RefusalDetail],
            QtiRefusals.Adaptive => S["it is adaptive (it changes as the learner answers), which no question type can hold"],
            QtiRefusals.Template => S["it is a template whose values are drawn when it is shown, which no question type can hold"],
            QtiRefusals.NoInteraction => S["it has no interaction"],
            QtiRefusals.SeveralInteractions => S["it has interactions of several kinds, or more than one, and a question holds one"],
            QtiRefusals.NoCorrectResponse => S["it has no correct response to score by"],
            QtiRefusals.CustomScoring => S["its response processing is not a standard template and uses {0}", item.RefusalDetail],
            QtiRefusals.UnsupportedScoring => S["its scoring cannot be kept by the question type: {0}", item.RefusalDetail],
            QtiRefusals.MarkupInText => S["a choice, an answer or a blank holds {0}, where the question type keeps plain text", item.RefusalDetail],
            QtiRefusals.UnsupportedAnswer => S["the accepted answer \"{0}\" holds a character a blank cannot keep (| [ or ])", item.RefusalDetail],
            QtiRefusals.MissingMedia => S["it refers to {0}, which the package does not have", item.RefusalDetail],
            QtiRefusals.UnsupportedMedia => S["it refers to {0}; only PNG, JPEG, GIF and WebP images in the package are imported", item.RefusalDetail],
            QtiRefusals.MediaTooLarge => S["the image {0} is larger than allowed", item.RefusalDetail],
            QtiRefusals.UnsupportedContent => S["it holds {0}, which is not imported", item.RefusalDetail],
            QtiRefusals.UnsupportedStructure => S["the question type cannot keep its {0}", item.RefusalDetail],
            _ => S["it could not be read ({0})", item.Refusal],
        };

    private sealed class Row
    {
        public string[] Values { get; } = new string[Columns.Count];

        public string this[string column]
        {
            set => Values[IndexOf(column)] = value;
        }

        private static int IndexOf(string column)
        {
            for (var i = 0; i < Columns.Count; i++)
            {
                if (Columns[i] == column)
                {
                    return i;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(column), column, null);
        }
    }
}

/// <summary>
/// A media file in the <see cref="QtiColumns.Media"/> cell.
/// </summary>
public sealed class QtiMediaCell
{
    /// <summary>
    /// Gets or sets the path of the file in the package.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the bytes, in Base64.
    /// </summary>
    public string Data { get; set; }
}
