using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Playwright.Models;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

/// <summary>
/// Shared scaffolding for all Playwright browser tools.
/// Resolves or creates the session and wires the stop token.
/// </summary>
public abstract class PlaywrightToolBase : AIFunction
{
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    protected async Task<IPlaywrightSession> ResolveSessionAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<PlaywrightToolBase>>();
        var sessionManager = arguments.Services.GetRequiredService<IPlaywrightSessionManager>();
        var requestResolver = arguments.Services.GetRequiredService<IPlaywrightSessionRequestResolver>();

        var chatSessionId = ResolveChatSessionId(arguments);
        if (string.IsNullOrWhiteSpace(chatSessionId))
        {
            logger.LogWarning("Playwright tool invoked but no chat session ID could be resolved.");
            return null;
        }

        var resource = AIInvocationScope.Current?.ToolExecutionContext?.Resource;
        var request = requestResolver.Resolve(resource, chatSessionId);
        if (request is null)
        {
            logger.LogWarning("Playwright tool invoked but no Playwright-enabled resource metadata was found.");
            return null;
        }

        return await sessionManager.GetOrCreateAsync(request, cancellationToken);
    }

    protected async ValueTask<object?> ExecuteSessionStepAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken,
        Func<IPlaywrightSession, CancellationToken, Task<object?>> action)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<PlaywrightToolBase>>();
        var session = await ResolveSessionAsync(arguments, cancellationToken);
        if (session is null)
        {
            return "Playwright session is not available. Enable Playwright on the current profile or interaction first.";
        }

        session.MarkRunning();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.StopToken);

        try
        {
            return await action(session, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            return "Playwright operation was stopped by the user.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Playwright tool step failed for session '{ChatSessionId}'.",
                session.SessionId);

            return Serialize(new
            {
                error = ex.Message,
                browserMode = "DedicatedBrowser",
                isAuthenticated = session.IsAuthenticated,
                observation = session.LastObservation,
            });
        }
        finally
        {
            session.MarkIdle();
        }
    }

    protected async ValueTask<object?> ExecuteObservationStepAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken,
        string intent,
        Func<IPlaywrightSession, CancellationToken, Task<PlaywrightObservation>> action)
    {
        return await ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var observation = await action(session, token);
            return Serialize(new
            {
                intent,
                browserMode = "DedicatedBrowser",
                isAuthenticated = session.IsAuthenticated,
                observation,
            });
        });
    }

    protected static string ResolveChatSessionId(AIFunctionArguments arguments)
    {
        if (AIInvocationScope.Current?.Items.TryGetValue(nameof(AIChatSession), out var sessionObject) == true
            && sessionObject is AIChatSession chatSession)
        {
            return chatSession.SessionId;
        }

        switch (AIInvocationScope.Current?.ToolExecutionContext?.Resource)
        {
            case ChatInteraction interaction when !string.IsNullOrWhiteSpace(interaction.ItemId):
                return interaction.ItemId;
            case CrestApps.OrchardCore.Models.CatalogItem item when !string.IsNullOrWhiteSpace(item.ItemId):
                return item.ItemId;
        }

        var httpContext = arguments.Services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        return httpContext?.User?.Identity?.Name;
    }

    protected static string ResolveAbsoluteUrl(IPlaywrightSession session, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (url.StartsWith('/'))
        {
            return PlaywrightSessionRequestResolver.CombineUrl(session.BaseUrl, url.TrimStart('/'));
        }

        return PlaywrightSessionRequestResolver.CombineUrl(session.BaseUrl, url);
    }

    protected static string Serialize(object value)
        => JsonSerializer.Serialize(value);
}
