using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Lists.Indexes;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// What the quiz editor's pickers read: the published questions, by title, for the fixed questions of a quiz, and the
/// tags and difficulty of a bank's questions, for its "Draw at random" rules. Only people who edit quizzes or see
/// question banks may read them, only from the banks they may view, and only titles, tags and difficulties leave, never
/// a key.
/// </summary>
[Admin]
public sealed class QuestionPickerController : Controller
{
    private const int MaxResults = 50;

    private readonly global::YesSql.ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly QuestionTypeOptions _questionTypes;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IAssessmentContentSource _contentSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionPickerController"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="questionTypes">The question types.</param>
    /// <param name="localizerFactory">The localizer factory, for the names of the question types.</param>
    /// <param name="contentSource">The assessment content source, which reads a bank's questions as an attempt does.</param>
    public QuestionPickerController(
        global::YesSql.ISession session,
        IAuthorizationService authorizationService,
        IOptions<QuestionTypeOptions> questionTypes,
        IStringLocalizerFactory localizerFactory,
        IAssessmentContentSource contentSource)
    {
        _session = session;
        _authorizationService = authorizationService;
        _questionTypes = questionTypes.Value;
        _localizerFactory = localizerFactory;
        _contentSource = contentSource;
    }

    /// <summary>
    /// Returns the published questions whose title contains the text, as <c>[{ value, text, secondaryText }]</c> for
    /// the ItemSelector component, the question type beside each.
    /// </summary>
    /// <param name="query">The text to look for; empty for the first questions by title.</param>
    [HttpGet]
    [Admin("learning/questions/search", "LearningQuestionSearch")]
    public async Task<IActionResult> Search(string query)
    {
        if (!await CanPickAsync())
        {
            return Forbid();
        }

        var contentTypes = _questionTypes.Types.Select(type => type.ContentType).ToArray();
        var text = query?.Trim();
        var all = _session.Query<ContentItem>();

        // Someone who may not view every bank (a course instructor) finds only the questions of the banks they may view.
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ViewQuestionBanks))
        {
            var bankIds = await ViewableBankIdsAsync();

            if (bankIds.Count == 0)
            {
                return Ok(Array.Empty<object>());
            }

            all = all.With<ContainedPartIndex>(index => index.ListContentItemId.IsIn(bankIds));
        }

        // A search inside titles can't seek, so it goes without the grouping (see AGENTS.md, YesSql index declarations).
        var questions = string.IsNullOrEmpty(text)
            ? all.With<ContentItemIndex>(index => index.Published && index.ContentType.IsIn(contentTypes))
            : all.NoIndexOptimizations().With<ContentItemIndex>(index => index.Published && index.ContentType.IsIn(contentTypes) && index.DisplayText.Contains(text));

        var found = await questions
            .OrderBy(index => index.DisplayText)
            .Take(MaxResults)
            .ListAsync(HttpContext.RequestAborted);

        return Ok(found.Select(question => new
        {
            value = question.ContentItemId,
            text = question.DisplayText,
            secondaryText = _questionTypes.FindByContentType(question.ContentType)?.DisplayName is { } name ? _localizerFactory.Localize(name).Value : null,
        }));
    }

    /// <summary>
    /// Returns the tags and the difficulty of each published question of a bank that an attempt can draw, as
    /// <c>{ questions: [{ tags, difficulty }] }</c>. The editor lists the bank's tags from it and counts what a rule can
    /// draw.
    /// </summary>
    /// <param name="bankId">The question bank.</param>
    [HttpGet]
    [Admin("learning/question-banks/{bankId}/draw-summary", "LearningQuestionBankDrawSummary")]
    public async Task<IActionResult> DrawSummary(string bankId)
    {
        if (!await CanPickAsync() || !await CanViewBankAsync(bankId))
        {
            return Forbid();
        }

        var questions = (await _contentSource.GetBankQuestionsAsync(bankId))
            .Where(question => _questionTypes.IsQuestion(question.ContentType))
            .Select(question => question.Get<QuestionPart>(nameof(QuestionPart)) ?? new QuestionPart())
            .Select(part => new
            {
                tags = part.Tags ?? [],
                difficulty = part.Difficulty,
            });

        return Ok(new { questions });
    }

    private async Task<bool> CanViewBankAsync(string bankId)
    {
        if (await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ViewQuestionBanks))
        {
            return true;
        }

        var bank = string.IsNullOrEmpty(bankId)
            ? null
            : await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == bankId && index.Published && index.ContentType == AssessmentsConstants.ContentTypes.QuestionBank).FirstOrDefaultAsync(HttpContext.RequestAborted);

        return bank is not null && await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, bank);
    }

    private async Task<IReadOnlyList<string>> ViewableBankIdsAsync()
    {
        var banks = await _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType == AssessmentsConstants.ContentTypes.QuestionBank)
            .ListAsync(HttpContext.RequestAborted);
        var ids = new List<string>();

        foreach (var bank in banks)
        {
            if (await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, bank))
            {
                ids.Add(bank.ContentItemId);
            }
        }

        return ids;
    }

    private async Task<bool> CanPickAsync()
        => await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ViewQuestionBanks)
        || await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageOwnCourses);
}
