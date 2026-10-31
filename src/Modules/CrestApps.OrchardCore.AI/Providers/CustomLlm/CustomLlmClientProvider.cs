using System.Net.WebSockets;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Providers.CustomLlm;

/// <summary>
/// Names the custom LLM provider to the completion client registration.
/// </summary>
public readonly struct CustomLlmClientMarker : IAIClientMarker
{
    /// <summary>
    /// The provider's client name.
    /// </summary>
    public const string Name = "CustomLlm";

    /// <inheritdoc/>
    public static string ClientName => Name;
}

/// <summary>
/// Makes chat clients that talk to a connection's WebSocket server. The connection's endpoint is the server's
/// <c>wss://</c> address and its API key, when set, is sent as a bearer token when the socket opens.
/// </summary>
public sealed class CustomLlmClientProvider : AIClientProviderBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomLlmClientProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public CustomLlmClientProvider(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    /// <inheritdoc/>
    protected override string GetProviderName()
        => CustomLlmClientMarker.Name;

    /// <inheritdoc/>
    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        var endpoint = ToWebSocketUri(connection.GetEndpoint());
        var apiKey = connection.GetApiKey(throwException: false);

        return new CustomLlmChatClient(
            async cancellationToken =>
            {
                var socket = new ClientWebSocket();

                if (!string.IsNullOrEmpty(apiKey))
                {
                    socket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                await socket.ConnectAsync(endpoint, cancellationToken);

                return socket;
            },
            deploymentName);
    }

    /// <inheritdoc/>
    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
        => throw new NotSupportedException("A custom LLM server answers chat turns only.");

#pragma warning disable MEAI001
    /// <inheritdoc/>
    protected override IImageGenerator GetImageGenerator(AIProviderConnectionEntry connection, string deploymentName)
        => throw new NotSupportedException("A custom LLM server answers chat turns only.");

    /// <inheritdoc/>
    protected override ISpeechToTextClient GetSpeechToTextClient(AIProviderConnectionEntry connection, string deploymentName)
        => throw new NotSupportedException("A custom LLM server answers chat turns only.");
#pragma warning restore MEAI001

    /// <summary>
    /// Turns an <c>https://</c> or <c>http://</c> address into its WebSocket form; a <c>wss://</c> or <c>ws://</c>
    /// address is kept.
    /// </summary>
    /// <param name="endpoint">The address.</param>
    /// <returns>The WebSocket address.</returns>
    public static Uri ToWebSocketUri(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var builder = new UriBuilder(endpoint);
        builder.Scheme = endpoint.Scheme switch
        {
            "https" => "wss",
            "http" => "ws",
            _ => endpoint.Scheme,
        };

        if (endpoint.IsDefaultPort)
        {
            builder.Port = -1;
        }

        return builder.Uri;
    }
}
