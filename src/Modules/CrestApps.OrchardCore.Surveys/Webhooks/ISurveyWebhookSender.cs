namespace CrestApps.OrchardCore.Surveys.Webhooks;

/// <summary>
/// Publishes a Surveys webhook event once the change that raised it is saved.
/// </summary>
internal interface ISurveyWebhookSender
{
    /// <summary>
    /// Publishes an event.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="eventId">The stable event id receivers de-duplicate on.</param>
    /// <param name="payload">The payload.</param>
    void Publish(string eventName, string eventId, object payload);
}
