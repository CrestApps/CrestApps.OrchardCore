using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Endpoints;

/// <summary>
/// Serves an outbound picture to the provider that delivers it. A picture message carries links rather than bytes,
/// and the provider fetches each link without signing in, so this endpoint is anonymous; what it serves is decided
/// entirely by the signed, expiring token in the link.
/// </summary>
internal static class MessagingAttachmentEndpoint
{
    public static IEndpointRouteBuilder AddMessagingAttachmentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(MessagingAttachmentLinks.PublicPathPrefix + "/{token}/{fileName?}", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string token,
        HttpContext httpContext,
        MessagingAttachmentLinks links,
        IMessagingAttachmentStore attachmentStore,
        ILogger<MessagingAttachmentLinks> logger)
    {
        if (!links.TryReadPublicToken(token, out var attachmentId, out var contentType) ||
            !MessagingImageFormat.SupportedContentTypes.Contains(contentType))
        {
            logger.LogWarning("Refused a request for a messaging picture whose link was not genuine or had expired.");

            return TypedResults.NotFound();
        }

        var bytes = await attachmentStore.ReadAsync(attachmentId, httpContext.RequestAborted);

        if (bytes is null)
        {
            logger.LogWarning("A provider asked for a messaging picture that is no longer stored.");

            return TypedResults.NotFound();
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Served a {Length}-byte messaging picture to the provider delivering it.", bytes.Length);
        }

        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        httpContext.Response.Headers.CacheControl = "private, max-age=3600";

        return TypedResults.File(bytes, contentType);
    }
}
