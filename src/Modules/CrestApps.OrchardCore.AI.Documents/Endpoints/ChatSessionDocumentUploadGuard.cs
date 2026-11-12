using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.Core.Throttling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
    /// How many documents one chat session may hold.
    /// </summary>
    public int MaxFilesPerSession { get; set; } = 20;
}

/// <summary>
/// Runs in front of CrestApps.Core's chat session upload endpoint. That endpoint creates and saves a session for a
/// profile id before it authorizes the caller, applies no session-start limit and accepts any number of files, so a
/// script that posts uploads in a loop costs model, embedding and storage money without a ceiling. This filter
/// authorizes first, applies the chat session-start limiter to an upload that starts a session, and caps the documents
/// of a session.
/// </summary>
internal sealed class ChatSessionDocumentUploadGuard : IEndpointFilter
{
    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;

        if (!request.HasFormContentType)
        {
            return await next(context);
        }

        var services = httpContext.RequestServices;
        var form = await request.ReadFormAsync(httpContext.RequestAborted);
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
            return Results.BadRequest("Choose a chat profile or an existing chat session to upload documents to.");
        }

        // The endpoint saves a new session before it authorizes, so the caller is authorized here first, every time.
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var authorization = await authorizationService.AuthorizeAsync(
            httpContext.User,
            new AIChatSessionDocumentAuthorizationContext(profile, session),
            AIChatDocumentOperations.ManageDocuments);

        if (!authorization.Succeeded)
        {
            return Results.Json(new { error = "You do not have permission to manage documents here." }, statusCode: StatusCodes.Status403Forbidden);
        }

        if ((session?.Documents?.Count ?? 0) + fileCount > options.MaxFilesPerSession)
        {
            return TooManyFiles(options);
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

    private static IResult TooManyFiles(ChatSessionDocumentUploadOptions options)
        => Results.BadRequest($"A chat session can hold at most {options.MaxFilesPerSession} documents.");
}
