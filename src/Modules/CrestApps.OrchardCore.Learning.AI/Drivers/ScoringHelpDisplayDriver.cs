using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.AI.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.AI.Drivers;

/// <summary>
/// Adds the scoring help panel next to the grade fields of a coursework submission, for graders who
/// may use it, while the grade can still be changed.
/// </summary>
internal sealed class ScoringHelpDisplayDriver : DisplayDriver<GradingAssistContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILearningAICompleter _completer;
    private readonly LinkGenerator _linkGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScoringHelpDisplayDriver"/> class.
    /// </summary>
    public ScoringHelpDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILearningAICompleter completer,
        LinkGenerator linkGenerator)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _completer = completer;
        _linkGenerator = linkGenerator;
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> DisplayAsync(GradingAssistContext model, BuildDisplayContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (!model.CanEdit || httpContext is null || !await _authorizationService.AuthorizeAsync(httpContext.User, LearningAIPermissions.UseScoringHelp))
        {
            return null;
        }

        var url = model.Kind switch
        {
            GradingAssistContext.SubmissionKind => _linkGenerator.GetPathByRouteValues(httpContext, "LearningAIScoringHelpSubmission", new { id = model.RecordId }),
            _ => null,
        };

        if (url is null)
        {
            return null;
        }

        var available = await _completer.IsAvailableAsync();

        return Initialize<ScoringHelpViewModel>("LearningScoringHelp", viewModel =>
        {
            viewModel.Url = url;
            viewModel.ModelAvailable = available;
            viewModel.HtmlId = "scoring-help-" + (model.RecordId ?? string.Empty);
            viewModel.PointsField = model.PointsField;
            viewModel.FeedbackField = model.FeedbackField;
            viewModel.RubricFieldPrefix = model.RubricFieldPrefix;
            viewModel.AssistedField = model.AssistedField;
        }).Location("Detail", "Content:5");
    }
}
