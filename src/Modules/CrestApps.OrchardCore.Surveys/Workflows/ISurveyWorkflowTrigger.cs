namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts Surveys workflow events once the change that raised them is saved.
/// </summary>
internal interface ISurveyWorkflowTrigger
{
    /// <summary>
    /// Starts an event after the current change is saved.
    /// </summary>
    /// <param name="eventName">The event activity name.</param>
    /// <param name="input">The input.</param>
    /// <param name="correlationId">The correlation id: the response id, or the survey id for survey events.</param>
    void Trigger(string eventName, IDictionary<string, object> input, string correlationId);
}
