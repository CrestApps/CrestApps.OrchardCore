using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Endpoints.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class AICompletionEndpoint
{
    public static IEndpointRouteBuilder AddAICompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("ai/completion/chat", HandleAsync<T>)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.AICompletionRoute)
            .DisableAntiforgery();

        return builder;
    }

    internal static async Task<IResult> HandleAsync<T>(
        IAuthorizationService authorizationService,
        IAIProfileManager chatProfileManager,
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        [FromKeyedServices("chat")] IAIMarkdownService markdownService,
        ILogger<T> logger,
        AICompletionRequest requestData)
    {
        if (string.IsNullOrWhiteSpace(requestData.ProfileId))
        {
            return TypedResults.BadRequest();
        }

        var profile = await chatProfileManager.FindByIdAsync(requestData.ProfileId);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var completionService = serviceProvider.GetKeyedService<IAICompletionService>(profile.Source);

        if (completionService is null)
        {
            return TypedResults.Problem($"Unable to find a chat completion service for the source: '{profile.Source}'.");
        }

        string userPrompt;
        bool isNew;
        AIChatSession chatSession;

        if (profile.Type == AIProfileType.TemplatePrompt)
        {
            if (string.IsNullOrWhiteSpace(requestData.SessionProfileId))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>()
                {
                    { nameof(requestData.SessionProfileId), ["SessionProfileId is required"] },
                });
            }

            var parentProfile = await chatProfileManager.FindByIdAsync(requestData.SessionProfileId);

            if (parentProfile is null)
            {
                return TypedResults.NotFound();
            }

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, requestData.SessionId, parentProfile, completionService, userPrompt: profile.Name);

            userPrompt = await liquidTemplateManager.RenderStringAsync(profile.PromptTemplate, NullEncoder.Default,
                new Dictionary<string, FluidValue>()
                {
                    ["Profile"] = new ObjectValue(profile),
                    ["Session"] = new ObjectValue(chatSession),
                });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(requestData.Prompt))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>()
                {
                    { nameof(requestData.Prompt), ["Prompt is required"] },
                });
            }

            userPrompt = requestData.Prompt.Trim();

            if (profile.Type == AIProfileType.Utility)
            {
                return await GetToolMessageAsync(completionService, profile, markdownService, userPrompt, requestData.IncludeHtmlResponse);
            }

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, requestData.SessionId, profile, completionService, userPrompt);
        }

        ChatCompletion completion = null;
        AIChatSessionPrompt message = null;
        ChatMessage bestChoice = null;

        if (profile.Type == AIProfileType.TemplatePrompt)
        {
            completion = await completionService.CompleteAsync([new ChatMessage(ChatRole.User, userPrompt)], new AICompletionContext()
            {
                Profile = profile,
                UserMarkdownInResponse = true,
            });

            bestChoice = completion?.Choices?.FirstOrDefault();

            message = new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.Assistant,
                IsGeneratedPrompt = true,
                Title = profile.PromptSubject,
                Content = !string.IsNullOrEmpty(bestChoice?.Text)
                ? bestChoice.Text
                : AIConstants.DefaultBlankMessage,
            };
        }
        else
        {
            // At this point, we complete as standard chat.
            chatSession.Prompts.Add(new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.User,
                Content = userPrompt,
            });

            var transcript = chatSession.Prompts.Where(x => !x.IsGeneratedPrompt)
                .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

            completion = await completionService.CompleteAsync(transcript, new AICompletionContext()
            {
                Profile = profile,
                Session = chatSession,
                UserMarkdownInResponse = requestData.IncludeHtmlResponse,
            });

            bestChoice = completion?.Choices?.FirstOrDefault();

            message = new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.Assistant,
                Title = profile.PromptSubject,
                Content = !string.IsNullOrEmpty(bestChoice?.Text)
                ? bestChoice.Text
                : AIConstants.DefaultBlankMessage,
            };
        }

        chatSession.Prompts.Add(message);

        await sessionManager.SaveAsync(chatSession);

        return TypedResults.Ok(new AIChatResponse
        {
            Success = completion?.Choices?.Any() ?? false,
            Type = profile.Type.ToString(),
            SessionId = chatSession.SessionId,
            IsNew = isNew,
            Message = new AIChatResponseMessageDetailed
            {
                Id = message.Id,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                HtmlContent = requestData.IncludeHtmlResponse && !string.IsNullOrEmpty(message.Content)
                ? markdownService.ToHtml(message.Content)
                : null,
            },
        });
    }

    private static async Task<(AIChatSession ChatSession, bool IsNewSession)> GetSessionsAsync(IAIChatSessionManager sessionManager, string sessionId, AIProfile profile, IAICompletionService completionService, string userPrompt)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existingChatSession = await sessionManager.FindAsync(sessionId);

            if (existingChatSession != null && existingChatSession.ProfileId == profile.Id)
            {
                return (existingChatSession, false);
            }
        }

        // At this point, we need to create a new session.
        var chatSession = await sessionManager.NewAsync(profile);

        if (profile.TitleType == AISessionTitleType.Generated)
        {
            var transcription = new List<ChatMessage>
            {
                new (ChatRole.User, userPrompt),
            };

            var profileClone = profile.Clone();

            profileClone.Alter<AIProfileMetadata>(m =>
            {
                m.SystemMessage = null;
                m.MaxTokens = 64; // 64 token to generate about 255 characters.
            });

            var context = new AICompletionContext()
            {
                Profile = profileClone,
                SystemMessage = AIConstants.TitleGeneratorSystemMessage,
            };

            var titleResponse = await completionService.CompleteAsync(transcription, context);

            // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
            chatSession.Title = titleResponse.Choices.Any()
                ? Str.Truncate(titleResponse.Choices.First().Text, 255)
                : Str.Truncate(userPrompt, 255);
        }

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Str.Truncate(userPrompt, 255);
        }

        return (chatSession, true);
    }

    private static async Task<IResult> GetToolMessageAsync(IAICompletionService completionService, AIProfile profile, IAIMarkdownService markdownService, string prompt, bool respondWithHtml)
    {
        var completion = await completionService.CompleteAsync([new ChatMessage(ChatRole.User, prompt)], new AICompletionContext()
        {
            Profile = profile,
            UserMarkdownInResponse = true,
        });

        var bestChoice = completion.Choices.FirstOrDefault();

        return TypedResults.Ok(new AIChatResponse
        {
            Success = completion.Choices.Any(),
            Type = nameof(AIProfileType.Utility),
            Message = new AIChatResponseMessageDetailed
            {
                Content = bestChoice?.Text ?? AIConstants.DefaultBlankMessage,
                HtmlContent = respondWithHtml && !string.IsNullOrEmpty(bestChoice?.Text)
                ? markdownService.ToHtml(bestChoice.Text)
                : null,
            },
        });
    }
}
