using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Endpoints;

/// <summary>
/// Serves an outbound file to the provider that delivers it. A message with attachments carries links rather than
/// bytes, and the provider fetches each link without signing in, so this endpoint is anonymous; what it serves is
/// decided entirely by the signed, expiring token in the link.
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
        var format = links.TryReadPublicToken(token, out var attachmentId, out var contentType)
            ? MessagingFileFormats.FindByContentType(contentType)
            : null;

        if (format is null)
        {
            logger.LogWarning("Refused a request for a messaging attachment whose link was not genuine or had expired.");

            return TypedResults.NotFound();
        }

        var bytes = await attachmentStore.ReadAsync(attachmentId, httpContext.RequestAborted);

        if (bytes is null)
        {
            logger.LogWarning("A provider asked for a messaging attachment that is no longer stored.");

            return TypedResults.NotFound();
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Served a {Length}-byte {Format} attachment to the provider delivering it.", bytes.Length, format.Name);
        }

        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        httpContext.Response.Headers.CacheControl = "private, max-age=3600";

        return TypedResults.File(bytes, format.ContentType);
    }
}
