using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Surveys.Api;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Limits the respondent routes when Orchard Core Rate Limits is on, in two groups:
/// <list type="bullet">
/// <item><see cref="RateLimitGroup"/>: the routes that start or reach a response (the landing, start, invitation,
/// unsubscribe, results and "my surveys" pages, and the API), <see cref="RequestsPerMinute"/> a minute per client (the
/// signed-in user, else the IP address), where guessing a password or a link matters.</item>
/// <item><see cref="ResponseRateLimitGroup"/>: the routes of a response already started (<c>/s/r/{responseId}</c>: its
/// pages, autosave, live evaluation, answer checks, the ending, the review and reopen), which need the response's owner
/// anyway. They are counted per caller and response (see <see cref="ResponsePartitionKey"/>), <see cref="ResponseRequestsPerMinute"/> a minute, so a class answering from
/// one network (a school or office address, a proxy) never shares one budget; <see cref="ClientCeilingGroup"/> adds a
/// generous ceiling per client of <see cref="ClientCeilingPerMinute"/> a minute.</item>
/// </list>
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RespondRateLimitsStartup : StartupBase
{
    /// <summary>
    /// The rate-limit group of the routes that start or reach a response.
    /// </summary>
    public const string RateLimitGroup = "Surveys.Respond";

    /// <summary>
    /// The rate-limit group of the routes of a response already started, counted per response.
    /// </summary>
    public const string ResponseRateLimitGroup = "Surveys.Respond.Response";

    /// <summary>
    /// The rate-limit group of the ceiling per client on the routes of started responses.
    /// </summary>
    public const string ClientCeilingGroup = "Surveys.Respond.Client";

    /// <summary>
    /// How many requests a client may make a minute to the routes that start or reach a response.
    /// </summary>
    public const int RequestsPerMinute = 120;

    /// <summary>
    /// How many requests a minute one response may take: far more than a person answering (a page post and its page,
    /// autosaves and live evaluations come to about ten a minute).
    /// </summary>
    public const int ResponseRequestsPerMinute = 120;

    /// <summary>
    /// How many requests a minute one client may make to the routes of started responses, whatever the response: enough
    /// for a class of a hundred behind one address.
    /// </summary>
    public const int ClientCeilingPerMinute = 2000;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options =>
        {
            options.AddGroupRateLimit(
                RateLimitGroup,
                RateLimitPartitionHelpers.CreateFixedWindowPolicy(
                    policyName: RateLimitGroup,
                    permitLimit: RequestsPerMinute,
                    window: TimeSpan.FromMinutes(1),
                    partitionKeySelector: RateLimitPartitionHelpers.GetClientPartitionKey));

            options.AddGroupRateLimit(
                ResponseRateLimitGroup,
                RateLimitPartitionHelpers.CreateFixedWindowPolicy(
                    policyName: ResponseRateLimitGroup,
                    permitLimit: ResponseRequestsPerMinute,
                    window: TimeSpan.FromMinutes(1),
                    partitionKeySelector: ResponsePartitionKey));

            options.AddGroupRateLimit(
                ClientCeilingGroup,
                RateLimitPartitionHelpers.CreateFixedWindowPolicy(
                    policyName: ClientCeilingGroup,
                    permitLimit: ClientCeilingPerMinute,
                    window: TimeSpan.FromMinutes(1),
                    partitionKeySelector: RateLimitPartitionHelpers.GetClientPartitionKey));
        });
    }

    /// <summary>
    /// Gets the key a request to a started response is counted under: the caller together with the response id of its
    /// route (cut to the longest id), else the client. The caller is the response's own resume token when the request
    /// carries it (the API header, or the resume cookie whose token names this response), else the signed-in user, else
    /// the IP address. The response id comes from the URL, before any ownership check, so a stranger who knows someone's
    /// response id only fills a budget of their own and can't lock the respondent out; a client that varies the id still
    /// meets <see cref="ClientCeilingGroup"/>.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The key.</returns>
    internal static string ResponsePartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var responseId = context.Request.RouteValues["responseId"]?.ToString();

        if (string.IsNullOrEmpty(responseId))
        {
            return RateLimitPartitionHelpers.GetClientPartitionKey(context);
        }

        if (responseId.Length > 64)
        {
            responseId = responseId[..64];
        }

        var token = ResumeTokenOf(context, responseId);
        var caller = token is null
            ? RateLimitPartitionHelpers.GetClientPartitionKey(context)
            : "token:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)), 0, 16);

        return "response:" + responseId + "|" + caller;
    }

    // The resume token of a response the request carries: the API header, or a respondent cookie whose token names the
    // response. Only its hash goes into the key.
    private static string ResumeTokenOf(HttpContext context, string responseId)
    {
        var prefix = responseId + ".";
        var header = context.Request.Headers[SurveysApiEndpoints.ResumeTokenHeader].ToString().Trim();

        if (header.Length is > 0 and <= 300 && header.StartsWith(prefix, StringComparison.Ordinal))
        {
            return header;
        }

        foreach (var (name, value) in context.Request.Cookies)
        {
            if (name.StartsWith(SurveyResumeCookies.CookiePrefix, StringComparison.Ordinal)
                && !name.StartsWith(SurveyResumeCookies.GenerationCookiePrefix, StringComparison.Ordinal)
                && value is { Length: > 0 and <= 300 }
                && value.StartsWith(prefix, StringComparison.Ordinal))
            {
                // The cookie holds the token and its generation; the token alone is the caller.
                var separator = value.LastIndexOf('~');

                return separator > 0 ? value[..separator] : value;
            }
        }

        return null;
    }
}
