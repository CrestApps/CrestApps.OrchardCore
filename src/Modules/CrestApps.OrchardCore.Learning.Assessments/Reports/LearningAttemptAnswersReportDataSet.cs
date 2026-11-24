using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Assessments.Reports;

/// <summary>
/// One row per question of each quiz attempt: the learner's answer and its score. A choice is named by the text the
/// learner saw; the answer key, the feedback and the grader guidance are not included.
/// </summary>
public sealed class LearningAttemptAnswersReportDataSet : IndexedReportDataSet<AssessmentAttempt, AssessmentAttemptIndex, ReportChildRow<AssessmentAttempt, AttemptItem>>, IAssessmentsReportDataSet
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "LearningAttemptAnswers";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningAttemptAnswersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningAttemptAnswersReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningAttemptAnswersReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Quiz answers"], stringLocalizer["One row per question of each attempt: the question, the learner's answer, the points and the score. Answer keys are not included."]), session, authorizationService, LearningConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Answer"].Value;

        AddField("AttemptId", S["Attempt ID"], DataFieldType.Text, row => row.Parent.ItemId, group).Identifier(new DataFieldReference(AssessmentsConstants.DataSourceName, LearningAttemptsReportDataSet.DataSetName, "AttemptId"));
        AddField("UserId", S["Learner"], DataFieldType.Text, row => row.Parent.UserId, group).Identifier(ReportDataReferences.User());
        AddField("QuizId", S["Quiz"], DataFieldType.Text, row => row.Parent.QuizId, group).Identifier(ReportDataReferences.ContentItem(AssessmentsConstants.ContentTypes.Quiz));
        AddField("Position", S["Position"], DataFieldType.Integer, row => row.Child.Index + 1, group);
        AddField("QuestionId", S["Question"], DataFieldType.Text, row => row.Child.QuestionId, group).Identifier();
        AddField("QuestionTitle", S["Question title when drawn"], DataFieldType.Text, row => row.Child.Title, group);
        AddField("QuestionType", S["Question type"], DataFieldType.Text, row => row.Child.QuestionType, group);
        AddField("Points", S["Points"], DataFieldType.Decimal, row => row.Child.Points, group);
        AddField("Score", S["Score"], DataFieldType.Decimal, row => row.Child.Score, group);
        AddField("Answered", S["Answered"], DataFieldType.Boolean, row => row.Child.Response is not null, group);
        AddField("AnsweredUtc", S["Answered at"], DataFieldType.DateTime, row => row.Child.Response?.AnsweredUtc, group);
        AddField("Answer", S["Answer"], DataFieldType.Text, row => Describe(row.Child), group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<AssessmentAttemptIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AssessmentAttemptIndex, string>>>(StringComparer.Ordinal)
        {
            ["AttemptId"] = index => index.ItemId,
            ["UserId"] = index => index.UserId,
            ["QuizId"] = index => index.QuizId,
        };

    /// <inheritdoc />
    protected override IEnumerable<ReportChildRow<AssessmentAttempt, AttemptItem>> GetRows(AssessmentAttempt document)
        => ReportChildRow<AssessmentAttempt, AttemptItem>.For(document, document.Items);

    // The learner's answer as they gave it: choices by the text they saw, texts, a number or matched pairs.
    private static string Describe(AttemptItem item)
    {
        var response = item.Response;

        if (response is null)
        {
            return null;
        }

        var parts = new List<string>();

        parts.AddRange(response.ChoiceIds.Select(id => item.Choices.FirstOrDefault(choice => choice.Id == id)?.Text ?? id));
        parts.AddRange(response.Texts.Where(text => !string.IsNullOrWhiteSpace(text)));

        if (response.Number is { } number)
        {
            parts.Add(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        parts.AddRange(response.Pairs.Select(pair => (item.Choices.FirstOrDefault(choice => choice.Id == pair.Key)?.Text ?? pair.Key) + " = " + (item.Answers.FirstOrDefault(answer => answer.Id == pair.Value)?.Text ?? pair.Value)));

        return string.Join(" | ", parts);
    }
}
