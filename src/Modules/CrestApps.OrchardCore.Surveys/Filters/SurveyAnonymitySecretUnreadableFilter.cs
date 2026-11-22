using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Filters;

/// <summary>
/// Shows respondents a plain "not available" page, with a 503 status, when an anonymous survey's secret can't be read on
/// this server (for example, Data Protection keys that aren't shared). The secret is never replaced silently; an
/// administrator resets it from the survey's access page.
/// </summary>
internal sealed class SurveyAnonymitySecretUnreadableFilter : IExceptionFilter
{
    private readonly IModelMetadataProvider _modelMetadataProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymitySecretUnreadableFilter"/> class.
    /// </summary>
    /// <param name="modelMetadataProvider">The model metadata provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SurveyAnonymitySecretUnreadableFilter(
        IModelMetadataProvider modelMetadataProvider,
        IStringLocalizer<SurveyAnonymitySecretUnreadableFilter> stringLocalizer)
    {
        _modelMetadataProvider = modelMetadataProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not SurveyAnonymitySecretUnreadableException)
        {
            return;
        }

        context.Result = new ViewResult
        {
            ViewName = "Message",
            StatusCode = StatusCodes.Status503ServiceUnavailable,
            ViewData = new ViewDataDictionary<SurveyMessageViewModel>(_modelMetadataProvider, context.ModelState)
            {
                Model = new SurveyMessageViewModel
                {
                    Title = S["Survey"],
                    Message = S["This survey isn't available right now. Please try again later."],
                },
            },
        };

        context.ExceptionHandled = true;
    }
}
