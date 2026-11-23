using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts Surveys workflow events in a deferred task, after the change that raised them is committed. An event of an
/// anonymous survey's response runs detached from the respondent's request: the workflow sees a background context of
/// the tenant with no user, no headers, no cookies and no query string (only the request's scheme, host and path base,
/// for links), so Liquid such as <c>{{ User.Identity.Name }}</c> or <c>{{ Request.Headers }}</c> can't write the
/// respondent's identity into the response or a webhook.
/// </summary>
internal sealed class DeferredSurveyWorkflowTrigger : ISurveyWorkflowTrigger
{
    /// <inheritdoc />
    public void Trigger(string eventName, IDictionary<string, object> input, string correlationId)
        => ShellScope.AddDeferredTask(scope => RunAsync(scope.ServiceProvider, eventName, input, correlationId));

    /// <summary>
    /// Runs an event, detached from the request when the input comes from an anonymous survey.
    /// </summary>
    /// <param name="services">The scope's services.</param>
    /// <param name="eventName">The event activity name.</param>
    /// <param name="input">The input.</param>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>A task.</returns>
    internal static async Task RunAsync(IServiceProvider services, string eventName, IDictionary<string, object> input, string correlationId)
    {
        var workflows = services.GetRequiredService<IWorkflowManager>();
        var accessor = IsAnonymous(input) ? services.GetService<IHttpContextAccessor>() : null;

        if (accessor is null)
        {
            await workflows.TriggerEventAsync(eventName, input, correlationId);

            return;
        }

        // The workflow runs on its own async flow that doesn't inherit the request's: replacing the HTTP context on the
        // request's flow would clear it for the rest of the request. The flow gets the shell scope back, the culture, and
        // a background context of the tenant, as Orchard's own background tasks get. Only the request's scheme, host and
        // path base are copied into it, so links in e-mails and webhooks point where the respondent was (the tenant's
        // background context alone may say localhost); nothing else of the respondent's request goes along.
        var scope = ShellScope.Current;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        var origin = accessor.HttpContext?.Request is { Host.HasValue: true } request
            ? (request.Scheme, request.Host, request.PathBase)
            : default((string Scheme, HostString Host, PathString PathBase)?);
        Task run;

        using (ExecutionContext.SuppressFlow())
        {
            run = Task.Run(async () =>
            {
                scope?.StartAsyncFlow();
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = uiCulture;

                var detached = scope?.ShellContext.CreateHttpContext() ?? new DefaultHttpContext();

                if (origin is { } from)
                {
                    detached.Request.Scheme = from.Scheme;
                    detached.Request.Host = from.Host;
                    detached.Request.PathBase = from.PathBase;
                }

                detached.RequestServices = services;
                detached.User = new ClaimsPrincipal(new ClaimsIdentity());
                accessor.HttpContext = detached;

                try
                {
                    await workflows.TriggerEventAsync(eventName, input, correlationId);
                }
                finally
                {
                    accessor.HttpContext = null;
                }
            });
        }

        await run;
    }

    private static bool IsAnonymous(IDictionary<string, object> input)
        => input is not null && input.TryGetValue(SurveyWorkflowInput.Anonymous, out var value) && value is true;
}
