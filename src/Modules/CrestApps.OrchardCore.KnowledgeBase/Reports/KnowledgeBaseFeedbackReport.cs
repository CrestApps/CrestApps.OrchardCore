using System.Globalization;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.KnowledgeBase.Reports;

/// <summary>
/// What readers answered to "Was this helpful?" in a period: for each article and each category, how many said yes and
/// no, the helpful rate and how many said what was missing, and those comments, newest first.
/// </summary>
internal sealed class KnowledgeBaseFeedbackReport : IReport
{
    // The most answers a run reads; a help center gets far fewer in the periods people look at.
    private const int MaxAnswers = 20000;

    // The most comments the report lists.
    private const int MaxComments = 200;

    private readonly ISession _session;
    private readonly KnowledgeBaseCategories _categories;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public KnowledgeBaseFeedbackReport(ISession session, KnowledgeBaseCategories categories, ILocalClock localClock, IStringLocalizer<KnowledgeBaseFeedbackReport> stringLocalizer)
    {
        _session = session;
        _categories = categories;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public static string Name => "knowledge-base-feedback";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<KnowledgeBaseFeedbackReport>("Knowledge Base Feedback");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<KnowledgeBaseFeedbackReport>("What readers answered to \"Was this helpful?\": yes, no and the helpful rate by article and by category, and what they said was missing.");

    public static string Category => ReportsConstants.Categories.General;

    public static Permission Permission => KnowledgeBasePermissions.ViewFeedback;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var range = context.Filter.GetDateRange();
        var from = range.FromUtc ?? DateTime.MinValue;
        var to = range.ToUtc ?? DateTime.MaxValue;

        var answers = (await _session.Query<KnowledgeArticleFeedback, KnowledgeArticleFeedbackIndex>(
                index => index.CreatedUtc >= from && index.CreatedUtc < to,
                KnowledgeBaseConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(MaxAnswers)
            .ListAsync(cancellationToken)).ToList();

        var tree = await _categories.GetTreeAsync();
        var times = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var answer in answers.Where(answer => !string.IsNullOrWhiteSpace(answer.Comment)).Take(MaxComments))
        {
            var local = await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(answer.CreatedUtc, DateTimeKind.Utc));
            times[answer.ItemId] = local.DateTime.ToString("g", CultureInfo.CurrentCulture);
        }

        return Build(answers, id => id is null ? null : tree.Find(id) is null ? null : tree.GetPathText(id, " › "), times, S);
    }

    /// <summary>
    /// Builds the report from the answers of the period, newest first.
    /// </summary>
    /// <param name="answers">The answers.</param>
    /// <param name="categoryName">The name of a category, with those above it, or <see langword="null"/> when it is gone.</param>
    /// <param name="times">When each listed comment was left, in the reader's time zone, by answer id.</param>
    /// <param name="S">The string localizer.</param>
    /// <returns>The report.</returns>
    internal static ReportDocument Build(IReadOnlyList<KnowledgeArticleFeedback> answers, Func<string, string> categoryName, IReadOnlyDictionary<string, string> times, IStringLocalizer S)
    {
        var yes = answers.Count(answer => answer.Helpful);
        var comments = answers.Count(answer => !string.IsNullOrWhiteSpace(answer.Comment));

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Answers"].Value, ReportFormat.Number(answers.Count)),
                new ReportMetric(S["Helpful"].Value, ReportFormat.Number(yes)),
                new ReportMetric(S["Not helpful"].Value, ReportFormat.Number(answers.Count - yes)),
                new ReportMetric(S["Helpful rate"].Value, Rate(yes, answers.Count)),
                new ReportMetric(S["Comments"].Value, ReportFormat.Number(comments), S["What readers said was missing after answering No."].Value),
            ]));

        if (answers.Count == 0)
        {
            var empty = ReportSection.ForTable(S["By article"].Value, [], []);
            empty.EmptyMessage = S["Nobody answered \"Was this helpful?\" in these days."].Value;

            return document.Add(empty);
        }

        // An article's newest answer has its current title and category.
        var articles = answers
            .GroupBy(answer => answer.ContentItemId, StringComparer.Ordinal)
            .Select(group => (Title: group.First().Title ?? group.Key, Category: categoryName(group.First().CategoryId), Answers: group.ToList()))
            .OrderBy(row => Helpful(row.Answers) / (double)row.Answers.Count)
            .ThenByDescending(row => row.Answers.Count)
            .ThenBy(row => row.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => Row([row.Title, row.Category ?? S["No category"].Value], row.Answers));

        var categories = answers
            .GroupBy(answer => categoryName(answer.CategoryId) ?? S["No category"].Value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => Row([group.Key], [.. group]));

        document
            .Add(ReportSection.ForTable(S["By article"].Value, [new ReportColumn(S["Article"].Value), new ReportColumn(S["Category"].Value), .. CountColumns(S)], articles))
            .Add(ReportSection.ForTable(S["By category"].Value, [new ReportColumn(S["Category"].Value), .. CountColumns(S)], categories));

        var listed = answers
            .Where(answer => !string.IsNullOrWhiteSpace(answer.Comment))
            .Take(MaxComments)
            .Select(answer => new ReportRow(
            [
                times.TryGetValue(answer.ItemId, out var time) ? time : string.Empty,
                answer.Title ?? answer.ContentItemId,
                answer.Comment,
                answer.UserName ?? S["A visitor"].Value,
            ]));

        var commentsTable = ReportSection.ForTable(S["What was missing"].Value,
            [
                new ReportColumn(S["Date"].Value),
                new ReportColumn(S["Article"].Value),
                new ReportColumn(S["Comment"].Value),
                new ReportColumn(S["Reader"].Value),
            ],
            listed);
        commentsTable.EmptyMessage = S["Nobody said what was missing in these days."].Value;

        return document.Add(commentsTable);
    }

    private static int Helpful(IEnumerable<KnowledgeArticleFeedback> answers)
        => answers.Count(answer => answer.Helpful);

    private static ReportRow Row(IEnumerable<string> names, List<KnowledgeArticleFeedback> answers)
    {
        var yes = Helpful(answers);

        return new ReportRow(
        [
            .. names,
            ReportFormat.Number(yes),
            ReportFormat.Number(answers.Count - yes),
            Rate(yes, answers.Count),
            ReportFormat.Number(answers.Count(answer => !string.IsNullOrWhiteSpace(answer.Comment))),
        ]);
    }

    private static IEnumerable<ReportColumn> CountColumns(IStringLocalizer S)
        =>
        [
            new ReportColumn(S["Helpful"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Not helpful"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Helpful rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Comments"].Value, ReportColumnAlign.End),
        ];

    private static string Rate(int yes, int total)
        => total == 0 ? "-" : ReportFormat.Percent(yes / (double)total);
}
