using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Surveys.Webhooks;

/// <summary>
/// Publishes Surveys webhook events in a deferred task, after the change is committed.
/// </summary>
internal sealed class DeferredSurveyWebhookSender : ISurveyWebhookSender
{
    /// <inheritdoc />
    public void Publish(string eventName, string eventId, object payload)
        => ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWebhookPublisher>().PublishAsync(eventName, payload, eventId));
}
