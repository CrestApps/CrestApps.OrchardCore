using System.Security.Claims;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Runs post-session analysis as no one. Its tools act on text a visitor wrote, and a tool that checks its caller reads
/// the user accessor's user, then the request's, so analysis re-run from an API call, a workflow or a close in someone's
/// request or hub invocation would otherwise carry that person's rights into whatever the visitor planted. The
/// background close task already runs with no user.
/// </summary>
internal static class PostSessionCaller
{
    /// <summary>
    /// Runs the analysis with the request's user and the user accessor's user replaced by an anonymous one, and puts the
    /// request's user back afterwards.
    /// </summary>
    /// <remarks>
    /// The accessor keeps its user in an async local, so the anonymous user set here stays in this method's flow. The
    /// accessor also detaches the user it held, as it does on every change, so code that runs after the analysis in the
    /// same invocation reads the request's user again, or none, never more rights than before.
    /// </remarks>
    /// <param name="httpContext">The current request, or <see langword="null"/> outside one.</param>
    /// <param name="userAccessor">The user accessor tools read their caller from first, or <see langword="null"/>.</param>
    /// <param name="analysis">The analysis to run.</param>
    public static async Task RunAsNoOneAsync(HttpContext httpContext, IUserAccessor userAccessor, Func<Task> analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var noOne = new ClaimsPrincipal(new ClaimsIdentity());
        var requestUser = httpContext?.User;

        if (httpContext is not null)
        {
            httpContext.User = noOne;
        }

        // An anonymous user, not null: a null user means a trusted server-side caller.
        if (userAccessor is not null)
        {
            userAccessor.User = noOne;
        }

        try
        {
            await analysis();
        }
        finally
        {
            if (httpContext is not null)
            {
                httpContext.User = requestUser;
            }
        }
    }
}
