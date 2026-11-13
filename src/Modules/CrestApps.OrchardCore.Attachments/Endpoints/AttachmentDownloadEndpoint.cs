using CrestApps.OrchardCore.Attachments.Core.Services;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.Attachments.Endpoints;

/// <summary>
/// Serves attached files at <c>/attachments/{id}</c>: to a signed-in user the record's module allows, or to anyone who
/// holds a signed link.
/// </summary>
internal static class AttachmentDownloadEndpoint
{
    /// <summary>
    /// The path the files are served under.
    /// </summary>
    public const string PathPrefix = "/attachments";

    /// <summary>
    /// Maps the endpoint.
    /// </summary>
    /// <param name="builder">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder AddAttachmentDownloadEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(PathPrefix + "/{id}", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string id,
        string token,
        bool? download,
        HttpContext httpContext,
        IAttachmentService attachmentService,
        IAttachmentAccessService accessService,
        SignedTokenService signedTokens)
    {
        var cancellationToken = httpContext.RequestAborted;
        var attachment = await attachmentService.FindAsync(id, cancellationToken);

        if (attachment is null)
        {
            return TypedResults.NotFound();
        }

        var signed = !string.IsNullOrEmpty(token)
            && string.Equals(signedTokens.Unprotect(DefaultAttachmentService.SignedLinkPurpose, token), attachment.ItemId, StringComparison.Ordinal);

        if (!signed && !await accessService.CanViewAsync(httpContext.User, attachment.Owner, cancellationToken))
        {
            // A guest without a link is asked to sign in; a signed-in user who may not see the record gets nothing,
            // not even whether the file exists.
            return httpContext.User.Identity?.IsAuthenticated == true ? TypedResults.NotFound() : TypedResults.Challenge();
        }

        var bytes = await attachmentService.ReadAsync(attachment, cancellationToken);

        if (bytes is null)
        {
            return TypedResults.NotFound();
        }

        // Only pictures recognized by their own bytes are shown in the page; everything else is a download, and the
        // browser is told not to guess another type.
        var inline = attachment.IsImage && download != true && FileFormats.FindByContentType(attachment.ContentType)?.IsImage == true;
        var headers = httpContext.Response.Headers;

        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = "private, no-store";
        headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";

        if (inline)
        {
            headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("inline") { FileNameStar = attachment.FileName }.ToString();

            return TypedResults.File(bytes, attachment.ContentType);
        }

        return TypedResults.File(bytes, attachment.ContentType, attachment.FileName);
    }
}
