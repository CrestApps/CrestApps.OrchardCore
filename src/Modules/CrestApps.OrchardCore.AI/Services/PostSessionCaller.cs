using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Runs post-session analysis as no one. Its tools act on text a visitor wrote, and a tool that checks its caller reads
/// the request's user, so analysis re-run from an API call, a workflow or a close in someone's request would otherwise
/// carry that person's rights into whatever the visitor planted. The background close task already runs with no user.
/// </summary>
internal static class PostSessionCaller
{
    /// <summary>
    /// Runs the analysis with the request's user replaced by an anonymous one, and puts the user back afterwards.
    /// </summary>
    /// <param name="httpContext">The current request, or <see langword="null"/> outside one.</param>
    /// <param name="analysis">The analysis to run.</param>
    public static async Task RunAsNoOneAsync(HttpContext httpContext, Func<Task> analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        if (httpContext is null)
        {
            await analysis();

            return;
        }

        var user = httpContext.User;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        try
        {
            await analysis();
        }
        finally
        {
            httpContext.User = user;
        }
    }
}
