using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// The work of the close and open survey tasks.
/// </summary>
internal static class SurveyStatusTask
{
    /// <summary>
    /// Changes the status of a survey.
    /// </summary>
    /// <param name="serviceProvider">The services.</param>
    /// <param name="evaluate">Evaluates a Liquid setting.</param>
    /// <param name="surveyIdExpression">The survey id setting.</param>
    /// <param name="status">The new status.</param>
    /// <param name="output">The output to fill.</param>
    /// <returns>The outcome name.</returns>
    public static async Task<string> ChangeAsync(IServiceProvider serviceProvider, Func<string, Task<string>> evaluate, string surveyIdExpression, SurveyStatus status, IDictionary<string, object> output)
    {
        var surveyId = await evaluate(surveyIdExpression);

        if (surveyId is null || serviceProvider.GetService<ISurveyManager>() is not { } manager)
        {
            return "Failed";
        }

        var error = await manager.ChangeStatusAsync(surveyId, status);

        output["SurveyId"] = surveyId;
        output["Status"] = status.ToString();
        output["Error"] = error.ToString();

        return error == SurveyErrorCode.None ? "Done" : "Failed";
    }
}
