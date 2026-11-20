using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys.Core.Services;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using CrestApps.OrchardCore.Learning.Surveys.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Surveys.Controllers;

/// <summary>
/// The totals page of a survey, for people who manage every course and for the staff of the survey's course with
/// Manage the courses you teach. Written answers are listed without names.
/// </summary>
[Admin]
public sealed class SurveyResultsController : Controller
{
    private readonly IContentManager _contentManager;
    private readonly ICourseStructureService _courseStructure;
    private readonly ISurveyResponseService _responses;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserSearchService _userSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResultsController"/> class.
    /// </summary>
    public SurveyResultsController(
        IContentManager contentManager,
        ICourseStructureService courseStructure,
        ISurveyResponseService responses,
        IAuthorizationService authorizationService,
        IUserSearchService userSearchService)
    {
        _contentManager = contentManager;
        _courseStructure = courseStructure;
        _responses = responses;
        _authorizationService = authorizationService;
        _userSearchService = userSearchService;
    }

    /// <summary>
    /// Shows the totals of a survey.
    /// </summary>
    /// <param name="surveyId">The survey item.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/surveys/{surveyId}/results", "LearningSurveyResults")]
    public async Task<IActionResult> Index(string surveyId)
    {
        var survey = string.IsNullOrEmpty(surveyId) ? null : await _contentManager.GetAsync(surveyId, VersionOptions.Latest);

        if (survey is null || !survey.Has<SurveyPart>())
        {
            return NotFound();
        }

        if (!await CanSeeResultsAsync(_courseStructure.GetCourseId(survey)))
        {
            return Forbid();
        }

        var part = survey.FindSurveyPart();
        var model = new SurveyResultsViewModel
        {
            Survey = survey,
            Part = part,
            Summary = await _responses.SummarizeAsync(survey.ContentItemId, HttpContext.RequestAborted),
        };

        // A survey that is not anonymous shows who wrote each written answer; the page lists the newest 1,000.
        var textQuestions = part.Questions.Where(question => question.Type == SurveyQuestionType.Text && !string.IsNullOrEmpty(question.Id)).ToList();

        if (!part.Anonymous && textQuestions.Count > 0)
        {
            var (_, responses) = await _responses.ListAsync(survey.ContentItemId, 0, 1000, HttpContext.RequestAborted);
            var users = await _userSearchService.GetAsync(responses.Select(response => response.UserId).Where(userId => !string.IsNullOrEmpty(userId)).Distinct(StringComparer.Ordinal), HttpContext.RequestAborted);
            var names = users.ToDictionary(user => user.UserId, user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName, StringComparer.Ordinal);

            foreach (var question in textQuestions)
            {
                model.NamedTextAnswers[question.Id] = responses
                    .Where(response => response.Answers.TryGetValue(question.Id, out var values) && values?.Count > 0)
                    .Select(response => (names.TryGetValue(response.UserId ?? string.Empty, out var name) ? name : response.UserId, response.Answers[question.Id][0]))
                    .ToList();
            }
        }

        return View(model);
    }

    private async Task<bool> CanSeeResultsAsync(string courseId)
    {
        if (await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageCourses))
        {
            return true;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(courseId)
            && !string.IsNullOrEmpty(userId)
            && await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageOwnCourses)
            && await _courseStructure.IsCourseStaffAsync(userId, courseId);
    }
}
