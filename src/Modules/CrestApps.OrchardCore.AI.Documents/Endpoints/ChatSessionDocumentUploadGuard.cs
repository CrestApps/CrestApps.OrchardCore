using System.Security.Claims;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

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
    /// an anonymous visitor's by the visitor's network. The package's session-start limiter counts only the uploads that
    /// start a session, and the hub lets a signed-in user start sessions freely. Its limit can be changed under
    /// <c>CrestApps_Throttling:AttemptPolicies:ai-chat-document-uploads</c>.
    /// </summary>
    public const string UploadPolicyName = "ai-chat-document-uploads";

    /// <summary>
    /// How many documents one chat session may hold.
    /// </summary>
    public int MaxFilesPerSession { get; set; } = 20;

    /// <summary>
    /// The default number of uploads a signed-in user, or an anonymous visitor's network, may make in
    /// <see cref="DefaultUploadWindow"/>.
    /// </summary>
    public const int DefaultUploadLimit = 60;

    /// <summary>
    /// The default window of <see cref="UploadPolicyName"/>.
    /// </summary>
    public static readonly TimeSpan DefaultUploadWindow = TimeSpan.FromHours(1);
}

/// <summary>
/// Runs in front of CrestApps.Core's chat session upload endpoint. That endpoint creates and saves a session for a
/// profile id before it authorizes the caller, applies no session-start limit and accepts any number of files, so a
/// script that posts uploads in a loop costs model, embedding and storage money without a ceiling. This filter
/// authorizes first, counts every upload (a signed-in user's by the user, an anonymous visitor's by network), applies the chat session-start limiter to an upload that
/// starts a session, and caps the documents of a session.
/// </summary>
/// <remarks>
/// CrestApps.Core #322 does the same three things in the endpoint itself. Once that package is referenced, delete this
/// filter, <see cref="ChatSessionDocumentUploadOptions"/> and the route group that adds it in the module's startup.
/// </remarks>
internal sealed class ChatSessionDocumentUploadGuard : IEndpointFilter
{
    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        var services = httpContext.RequestServices;
        var S = services.GetRequiredService<IStringLocalizer<ChatSessionDocumentUploadGuard>>();

        // Documents are uploaded as a form. A request that is not one carries no profile and no session, so it is read
        // as an empty form and refused by the profile check below: no branch on the content type skips the checks.
        var form = request.HasFormContentType
            ? await request.ReadFormAsync(httpContext.RequestAborted)
            : FormCollection.Empty;
        var fileCount = form.Files.Count;
        var options = services.GetRequiredService<IOptions<ChatSessionDocumentUploadOptions>>().Value;
        // A session id counts only when the session exists: a made-up one would otherwise skip every check below, and the
        // endpoint would start a new session for the profile the caller named.
        var sessionId = form["sessionId"].ToString();
        var session = string.IsNullOrWhiteSpace(sessionId)
            ? null
            : await services.GetRequiredService<IAIChatSessionManager>().FindAsync(sessionId);

        // An existing session is checked against its own profile, never the one the form names.
        var profileId = session?.ProfileId ?? form["profileId"].ToString();
        var profile = string.IsNullOrWhiteSpace(profileId)
            ? null
            : await services.GetRequiredService<IAIProfileManager>().FindByIdAsync(profileId);

        if (profile is null)
        {
            return Results.BadRequest(S["Choose a chat profile or an existing chat session to upload documents to."].Value);
        }

        // The endpoint saves a new session before it authorizes, so the caller is authorized here first, every time.
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var authorization = await authorizationService.AuthorizeAsync(
            httpContext.User,
            new AIChatSessionDocumentAuthorizationContext(profile, session),
            AIChatDocumentOperations.ManageDocuments);

        if (!authorization.Succeeded)
        {
            return Results.Json(new { error = S["You do not have permission to manage documents here."].Value }, statusCode: StatusCodes.Status403Forbidden);
        }

        if ((session?.Documents?.Count ?? 0) + fileCount > options.MaxFilesPerSession)
        {
            return Results.BadRequest(S["A chat session can hold at most {0} documents.", options.MaxFilesPerSession].Value);
        }

        // Every upload counts against a policy of its own, to a new session or an existing one: counting only the uploads
        // that start a session left a loop of hub sessions, each given the most documents, without a ceiling, and an
        // anonymous visitor could upload, remove and upload again to one session, each upload running extraction and
        // embeddings. A signed-in user is counted by the user, an anonymous visitor by the visitor's network.
        if (services.GetService<IAttemptLimiter>() is { } attemptLimiter)
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

        if (session is not null)
        {
            return await next(context);
        }

        // An upload without a session starts one, so it counts against the same limit as a chat that starts one.
        var sessionStartLimiter = services.GetService<IChatSessionStartRateLimiter>();

        if (sessionStartLimiter is not null)
        {
            var visitor = services.GetService<IAIVisitorIdentityResolver>()?.Resolve() ?? new AIVisitorIdentity();
            var result = await sessionStartLimiter.EvaluateAsync(new PromptSecurityContext
            {
                ProfileId = profile.ItemId,
                Profile = profile,
                User = httpContext.User,
                VisitorId = visitor.VisitorId,
                RemoteAddressHash = visitor.RemoteAddressHash,
                RemoteAddress = visitor.RemoteAddress,
            }, httpContext.RequestAborted);

            if (result.IsThrottled)
            {
                await ThrottlingServiceCollectionExtensions.WriteTooManyRequestsAsync(httpContext, TimeSpan.FromSeconds(result.RetryAfterSeconds), httpContext.RequestAborted);

                return Results.Empty;
            }
        }

        return await next(context);
    }
}
