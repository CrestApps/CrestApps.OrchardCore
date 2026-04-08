using System.Text.Json;
using A2A;
using CrestApps.Core.Mvc.Samples.A2AClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CrestApps.Core.Mvc.Samples.A2AClient.Pages;

public sealed class AgentsModel : PageModel
{
    private readonly A2AClientFactory _clientFactory;
    private readonly ILogger<AgentsModel> _logger;

    public AgentsModel(A2AClientFactory clientFactory, ILogger<AgentsModel> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public List<AgentCard> AgentCards { get; private set; } = [];

    public string ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAgentCardsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        await LoadAgentCardsAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostSendMessageAsync(string agentUrl, string agentName, string message, bool stream, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new JsonResult(new { error = "Message is required." });
        }

        try
        {
            var client = _clientFactory.Create(agentUrl);

            var agentMessage = new AgentMessage
            {
                Role = MessageRole.User,
                MessageId = Guid.NewGuid().ToString(),
                ContextId = Guid.NewGuid().ToString(),
                Parts = [new TextPart { Text = message }],
            };

            if (!string.IsNullOrWhiteSpace(agentName))
            {
                agentMessage.Metadata = new Dictionary<string, JsonElement>
                {
                    ["agentName"] = JsonSerializer.SerializeToElement(agentName),
                };
            }

            var sendParams = new MessageSendParams
            {
                Message = agentMessage,
            };

            if (stream)
            {
                return new StreamingA2AResult(client, sendParams, _logger);
            }

            var response = await client.SendMessageAsync(sendParams, cancellationToken);

            var responseText = ExtractTextFromResponse(response);

            return new JsonResult(new { response = responseText ?? "The agent did not produce a text response." });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(ex, "Authentication failed when communicating with the A2A agent.");

            return new JsonResult(new
            {
                error = "Authentication failed (401 Unauthorized). " +
                "The A2A host requires authentication. Check the agent card's security schemes for details."
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(ex, "Access denied when communicating with the A2A agent.");

            return new JsonResult(new
            {
                error = "Access denied (403 Forbidden). " +
                "You do not have permission to access this agent."
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new JsonResult(new
            {
                error = "The A2A host returned a 404 Not Found response. " +
                "Please ensure the A2A Host feature is enabled on the default tenant " +
                "(Configuration > Features > search for 'A2A Host')."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to communicate with the A2A agent at '{AgentUrl}'.", agentUrl);

            return new JsonResult(new { error = $"An error occurred while communicating with the agent: {ex.Message}" });
        }
    }

    private static string ExtractTextFromResponse(A2AResponse response)
    {
        if (response is AgentMessage message)
        {
            var texts = message.Parts?.OfType<TextPart>().Select(p => p.Text);

            if (texts?.Any() == true)
            {
                return string.Join(string.Empty, texts);
            }
        }
        else if (response is AgentTask task)
        {
            if (task.Artifacts?.Count > 0)
            {
                var artifactTexts = task.Artifacts
                    .SelectMany(a => a.Parts?.OfType<TextPart>() ?? [])
                    .Select(p => p.Text);

                var combined = string.Join(string.Empty, artifactTexts);

                if (!string.IsNullOrEmpty(combined))
                {
                    return combined;
                }
            }

            if (task.Status.Message?.Parts is not null)
            {
                var statusTexts = task.Status.Message.Parts.OfType<TextPart>().Select(p => p.Text);

                var combined = string.Join(string.Empty, statusTexts);

                if (!string.IsNullOrEmpty(combined))
                {
                    return combined;
                }
            }
        }

        return null;
    }

    private async Task LoadAgentCardsAsync(CancellationToken cancellationToken)
    {
        try
        {
            AgentCards = await _clientFactory.GetAgentCardsAsync(cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            ErrorMessage = "The A2A host returned a 404 Not Found response. " +
            "Please ensure the A2A Host feature is enabled on the default tenant " +
            "(Configuration > Features > search for 'A2A Host').";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent cards.");
            ErrorMessage = $"An error occurred while loading agent cards: {ex.Message}";
        }
    }
    /// <summary>
    /// Custom <see cref="IActionResult"/> that streams A2A events as text/event-stream
    /// so the browser receives chunks incrementally.
    /// </summary>
    private sealed class StreamingA2AResult : IActionResult
    {
        private readonly A2A.A2AClient _client;
        private readonly MessageSendParams _sendParams;
        private readonly ILogger _logger;

        public StreamingA2AResult(A2A.A2AClient client, MessageSendParams sendParams, ILogger logger)
        {
            _client = client;
            _sendParams = sendParams;
            _logger = logger;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var httpResponse = context.HttpContext.Response;
            httpResponse.ContentType = "text/event-stream";
            httpResponse.Headers.CacheControl = "no-cache";
            httpResponse.Headers.Connection = "keep-alive";

            var cancellationToken = context.HttpContext.RequestAborted;

            try
            {
                await foreach (var sseItem in _client.SendMessageStreamingAsync(_sendParams, cancellationToken))
                {
                    var a2aEvent = sseItem.Data;
                    string chunk = null;

                    if (a2aEvent is TaskArtifactUpdateEvent artifactUpdate)
                    {
                        chunk = string.Join(string.Empty,
                        artifactUpdate.Artifact?.Parts?.OfType<TextPart>().Select(p => p.Text) ?? []);
                    }
                    else if (a2aEvent is TaskStatusUpdateEvent statusUpdate)
                    {
                        if (statusUpdate.Final)
                        {
                            // If the task failed, send the error message.

                            if (statusUpdate.Status.State == TaskState.Failed)
                            {
                                var errorText = statusUpdate.Status.Message?.Parts
                                ?.OfType<TextPart>()
                                    .Select(p => p.Text)
                                    .FirstOrDefault() ?? "Agent task failed.";

                                await httpResponse.WriteAsync($"data: [ERROR]{errorText}\n\n", cancellationToken);
                                await httpResponse.Body.FlushAsync(cancellationToken);
                                break;
                            }

                            await httpResponse.WriteAsync("data: [DONE]\n\n", cancellationToken);
                            await httpResponse.Body.FlushAsync(cancellationToken);
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        var escaped = chunk.Replace("\n", "\ndata: ");
                        await httpResponse.WriteAsync($"data: {escaped}\n\n", cancellationToken);
                        await httpResponse.Body.FlushAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected.
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning(ex, "Authentication failed during streaming.");
                await WriteErrorAsync(httpResponse, "Authentication failed (401 Unauthorized).");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning(ex, "Access denied during streaming.");
                await WriteErrorAsync(httpResponse, "Access denied (403 Forbidden).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during A2A streaming.");
                await WriteErrorAsync(httpResponse, ex.Message);
            }
        }

        private static async Task WriteErrorAsync(HttpResponse httpResponse, string message)
        {
            try
            {
                await httpResponse.WriteAsync($"data: [ERROR]{message}\n\n", CancellationToken.None);
                await httpResponse.Body.FlushAsync(CancellationToken.None);
            }
            catch
            {
                // Response may already be completed.
            }
        }
    }
}
