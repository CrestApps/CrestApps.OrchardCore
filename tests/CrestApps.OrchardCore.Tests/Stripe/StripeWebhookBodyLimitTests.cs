using System.Reflection;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// SEC-C17c: the Stripe webhook checks that a signature header is there before it reads the body, and reads at most
/// 512 KB of it, so an anonymous caller can't make the store buffer a body up to the server's limit.
/// </summary>
public sealed class StripeWebhookBodyLimitTests
{
    [Fact]
    public async Task HandleAsync_WithoutASignatureHeader_ReadsNothing()
    {
        // Arrange
        var body = new EndlessStream();
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Body = body;

        // Act
        var result = await InvokeAsync(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, body.BytesProduced);
    }

    [Fact]
    public async Task HandleAsync_ABodyOverTheLimit_IsRefusedAfterABoundedRead()
    {
        // Arrange
        var body = new EndlessStream();
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Headers["Stripe-Signature"] = "t=1,v1=abc";
        httpContext.Request.Body = body;

        // Act
        var result = await InvokeAsync(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.True(body.BytesProduced <= 1024 * 1024, $"Read {body.BytesProduced} bytes of a body limited to 512 KB.");
    }

    private static async Task<IResult> InvokeAsync(HttpContext httpContext)
    {
        var method = typeof(CreateWebhookEndpoint).GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(typeof(StripeWebhookBodyLimitTests));
        var arguments = method.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(IHttpContextAccessor)
                ? new HttpContextAccessor { HttpContext = httpContext }
                : parameter.ParameterType == typeof(CancellationToken)
                    ? (object)TestContext.Current.CancellationToken
                    : parameter.ParameterType == typeof(Microsoft.Extensions.Logging.ILogger<StripeWebhookBodyLimitTests>)
                        ? NullLogger<StripeWebhookBodyLimitTests>.Instance
                        : null)
            .ToArray();

        return await (Task<IResult>)method.Invoke(null, arguments);
    }
}
