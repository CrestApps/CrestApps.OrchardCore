using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Turns the result of a response operation into a task outcome and output.
/// </summary>
internal static class ResponseTaskOutcome
{
    /// <summary>
    /// Writes the result to the output and gives the outcome.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="responseId">The response id.</param>
    /// <param name="output">The output.</param>
    /// <returns>The outcome name.</returns>
    public static string From(SurveyResponseResult result, string responseId, IDictionary<string, object> output)
    {
        output["ResponseId"] = responseId;
        output["Status"] = result?.Response?.Status.ToString();
        output["Score"] = result?.Response?.Score;
        output["Error"] = result?.Error.ToString();

        if (result is null)
        {
            return "Failed";
        }

        return result.Error switch
        {
            SurveyErrorCode.None => "Done",
            SurveyErrorCode.NotFound => "NotFound",
            _ => "Failed",
        };
    }
}
