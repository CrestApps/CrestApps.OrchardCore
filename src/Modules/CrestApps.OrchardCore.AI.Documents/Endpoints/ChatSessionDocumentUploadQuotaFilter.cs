using System.Security.Claims;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Documents.Endpoints;

/// <summary>
/// The limits of chat session document uploads, set under <c>CrestApps_AI:ChatSessionDocuments</c>.
/// </summary>
internal sealed class ChatSessionDocumentUploadOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "CrestApps_AI:ChatSessionDocuments";

    /// <summary>
    /// The rate-limit group of the session document routes, applied when Orchard Core's Rate Limits feature is
    /// enabled.
    /// </summary>
    public const string RateLimitGroup = "ai-chat-documents";

    /// <summary>
    /// The attempt policy that counts every upload, to a new session or an existing one: a signed-in user's by the user,
    /// an anonymous visitor's by the visitor's network. CrestApps.Core's endpoint applies the session-start limiter only to
    /// an upload that starts a session, and that limiter lets a signed-in user start sessions freely. Its limit can be
    /// changed under <c>CrestApps_Throttling:AttemptPolicies:ai-chat-document-uploads</c>.
    /// </summary>
    public const string UploadPolicyName = "ai-chat-document-uploads";

    /// <summary>
    /// The default number of uploads a signed-in user, or an anonymous visitor's network, may make in
    /// <see cref="DefaultUploadWindow"/>.
    /// </summary>
    public const int DefaultUploadLimit = 60;

    /// <summary>
    /// The default window of <see cref="UploadPolicyName"/>.
    /// </summary>
    public static readonly TimeSpan DefaultUploadWindow = TimeSpan.FromHours(1);

    /// <summary>
    /// How many documents one chat session may hold. The module copies it onto CrestApps.Core's
    /// <c>ChatDocumentsOptions.MaxDocumentsPerSession</c>, which the upload endpoint enforces.
    /// </summary>
    public int MaxFilesPerSession { get; set; } = 20;
}

/// <summary>
/// Counts every chat session document upload against the <see cref="ChatSessionDocumentUploadOptions.UploadPolicyName"/>
/// attempt policy before CrestApps.Core's upload endpoint runs. The endpoint itself authorizes the caller before it saves
/// a session, applies the session-start limiter to an upload that starts one and caps the documents of a session; it has
/// no ceiling on uploads to existing sessions, so a loop of uploads, removals and uploads again would run extraction and
/// embeddings without end.
/// </summary>
internal sealed class ChatSessionDocumentUploadQuotaFilter : IEndpointFilter
{
    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // The endpoint reads the request as a form and throws on anything else; a request that is not one is answered
        // here instead of failing as a server error.
        if (!httpContext.Request.HasFormContentType)
        {
            var S = httpContext.RequestServices.GetRequiredService<IStringLocalizer<ChatSessionDocumentUploadQuotaFilter>>();

            return Results.BadRequest(S["Upload documents as a form."].Value);
        }

        if (httpContext.RequestServices.GetService<IAttemptLimiter>() is { } attemptLimiter)
        {
            var key = httpContext.User?.Identity?.IsAuthenticated == true &&
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId
                    ? "user:" + userId
                    : "network:" + ClientNetworkKey.NetworkOnly(httpContext);
            var attempt = await attemptLimiter.AcquireAsync(ChatSessionDocumentUploadOptions.UploadPolicyName, key, httpContext.RequestAborted);

            if (!attempt.Allowed)
            {
                await ThrottlingServiceCollectionExtensions.WriteTooManyRequestsAsync(httpContext, attempt.RetryAfter, httpContext.RequestAborted);

                return Results.Empty;
            }
        }

        return await next(context);
    }
}
