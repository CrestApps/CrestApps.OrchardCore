using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// What the quiz editor's pickers search: the published questions, by title, for the fixed questions of a quiz. Only
/// people who edit quizzes or see question banks may search, and only titles leave, never a key.
/// </summary>
[Admin]
public sealed class QuestionPickerController : Controller
{
    private const int MaxResults = 50;

    private readonly global::YesSql.ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly QuestionTypeOptions _questionTypes;
    private readonly IStringLocalizerFactory _localizerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionPickerController"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="questionTypes">The question types.</param>
    /// <param name="localizerFactory">The localizer factory, for the names of the question types.</param>
    public QuestionPickerController(
        global::YesSql.ISession session,
        IAuthorizationService authorizationService,
        IOptions<QuestionTypeOptions> questionTypes,
        IStringLocalizerFactory localizerFactory)
    {
        _session = session;
        _authorizationService = authorizationService;
        _questionTypes = questionTypes.Value;
        _localizerFactory = localizerFactory;
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

    private async Task<bool> CanPickAsync()
        => await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ViewQuestionBanks)
        || await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageOwnCourses);
}
