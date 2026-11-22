using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts Surveys workflow events in a deferred task, after the change that raised them is committed. An event of an
/// anonymous survey's response runs with no user: the request's signed-in user is swapped for an anonymous principal
/// while the workflow runs, so Liquid such as <c>{{ User.Identity.Name }}</c> can't write the respondent's identity into
/// the response or a webhook.
/// </summary>
internal sealed class DeferredSurveyWorkflowTrigger : ISurveyWorkflowTrigger
{
    /// <inheritdoc />
    public void Trigger(string eventName, IDictionary<string, object> input, string correlationId)
        => ShellScope.AddDeferredTask(scope => RunAsync(scope.ServiceProvider, eventName, input, correlationId));

    /// <summary>
    /// Runs an event, without the signed-in user when the input comes from an anonymous survey.
    /// </summary>
    /// <param name="services">The scope's services.</param>
    /// <param name="eventName">The event activity name.</param>
    /// <param name="input">The input.</param>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>A task.</returns>
    internal static async Task RunAsync(IServiceProvider services, string eventName, IDictionary<string, object> input, string correlationId)
    {
        var workflows = services.GetRequiredService<IWorkflowManager>();
        var httpContext = IsAnonymous(input) ? services.GetService<IHttpContextAccessor>()?.HttpContext : null;

        if (httpContext is null)
        {
            await workflows.TriggerEventAsync(eventName, input, correlationId);

            return;
        }

        var user = httpContext.User;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        try
        {
            await workflows.TriggerEventAsync(eventName, input, correlationId);
        }
        finally
        {
            httpContext.User = user;
        }
    }

    private static bool IsAnonymous(IDictionary<string, object> input)
        => input is not null && input.TryGetValue(SurveyWorkflowInput.Anonymous, out var value) && value is true;
}
