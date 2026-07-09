using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class ProviderVoiceWebhookEndpoint
{
    public static IEndpointRouteBuilder AddProviderVoiceWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/contact-center/voice/webhook/{provider}", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        string provider,
        IProviderVoiceWebhookProcessor processor,
        HttpContext httpContext)
    {
        using var reader = new StreamReader(httpContext.Request.Body);
        var body = await reader.ReadToEndAsync(httpContext.RequestAborted);

        var request = new ProviderVoiceWebhookRequest
        {
            Provider = provider,
            Body = body,
        };

        foreach (var header in httpContext.Request.Headers)
        {
            request.Headers[header.Key] = header.Value.ToString();
        }

        foreach (var query in httpContext.Request.Query)
        {
            request.Query[query.Key] = query.Value.ToString();
        }

        var outcome = await processor.ProcessAsync(request, httpContext.RequestAborted);

        return outcome.Status switch
        {
            ProviderVoiceWebhookStatus.Accepted => TypedResults.Ok(new { processed = outcome.ProcessedCount }),
            ProviderVoiceWebhookStatus.UnknownProvider => TypedResults.NotFound(),
            ProviderVoiceWebhookStatus.InvalidSignature => TypedResults.Unauthorized(),
            ProviderVoiceWebhookStatus.MissingIdempotencyKey => TypedResults.BadRequest(),
            _ => TypedResults.BadRequest(),
        };
    }
}
