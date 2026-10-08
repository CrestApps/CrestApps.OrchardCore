using System.Net.Http;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Adds the <see cref="EgressGuard"/> to every named and unnamed HTTP client of a parent or child tenant.
/// </summary>
internal sealed class EgressGuardHttpClientFactoryOptionsSetup : IConfigureNamedOptions<HttpClientFactoryOptions>
{
    private readonly EgressGuard _guard;

    /// <summary>
    /// Initializes a new instance of the <see cref="EgressGuardHttpClientFactoryOptionsSetup"/> class.
    /// </summary>
    /// <param name="guard">The egress guard.</param>
    public EgressGuardHttpClientFactoryOptionsSetup(EgressGuard guard)
    {
        _guard = guard;
    }

    /// <inheritdoc/>
    public void Configure(string name, HttpClientFactoryOptions options)
    {
        if (!_guard.IsActive)
        {
            return;
        }

        options.HttpMessageHandlerBuilderActions.Add(builder =>
        {
            // The delegating handler checks the destination of every request, including a request sent through a
            // proxy. The connect callback checks the address the connection really opens to, which stops DNS rebinding.
            builder.AdditionalHandlers.Insert(0, new EgressGuardDelegatingHandler(_guard));

            switch (builder.PrimaryHandler)
            {
                case SocketsHttpHandler sockets:
                    sockets.ConnectCallback ??= _guard.ConnectAsync;
                    break;

                case HttpClientHandler handler when IsReplaceable(handler):
                    builder.PrimaryHandler = new SocketsHttpHandler
                    {
                        AllowAutoRedirect = handler.AllowAutoRedirect,
                        AutomaticDecompression = handler.AutomaticDecompression,
                        ConnectCallback = _guard.ConnectAsync,
                        MaxAutomaticRedirections = handler.MaxAutomaticRedirections,
                        MaxConnectionsPerServer = handler.MaxConnectionsPerServer,
                        UseCookies = handler.UseCookies,
                        CookieContainer = handler.CookieContainer,
                        UseProxy = handler.UseProxy,
                    };

                    break;
            }
        });
    }

    /// <inheritdoc/>
    public void Configure(HttpClientFactoryOptions options)
        => Configure(Options.DefaultName, options);

    private static bool IsReplaceable(HttpClientHandler handler)
    {
        // A handler that a client configured with a proxy, credentials or certificates keeps its own transport, and only
        // the delegating handler checks it.
        return handler.GetType() == typeof(HttpClientHandler) &&
            handler.Proxy is null &&
            handler.Credentials is null &&
            handler.ServerCertificateCustomValidationCallback is null &&
            handler.ClientCertificates.Count == 0;
    }
}
