using CrestApps.OrchardCore.AI.Azure.Core;
using CrestApps.OrchardCore.AI.Core;
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
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class AIChatCompletionEndpoint
{
    public static IEndpointRouteBuilder AddAIChatCompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("AI/Chat/Completion", HandleAsync<T>)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.ChatCompletionRouteName)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync<T>(
        IAuthorizationService authorizationService,
        IAIChatProfileManager chatProfileManager,
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        [FromKeyedServices("chat")] IAIMarkdownService markdownService,
        ILogger<T> logger,
        OpenAIChatCompletionRequest requestData)
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

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIChatPermissions.QueryAnyAIChatProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var completionService = serviceProvider.GetKeyedService<IAIChatCompletionService>(profile.Source);

        if (completionService is null)
        {
            return TypedResults.Problem($"Unable to find a chat completion service for the source: '{profile.Source}'.");
        }

        string userPrompt;
        bool isNew;
        AIChatSession chatSession;

        if (profile.Type == AIChatProfileType.TemplatePrompt)
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

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, chatProfileManager, requestData.SessionId, parentProfile, completionService, userPrompt: profile.Name);

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

            if (profile.Type == AIChatProfileType.Utility)
            {
                return await GetToolMessageAsync(completionService, profile, markdownService, userPrompt, requestData.IncludeHtmlResponse);
            }

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, chatProfileManager, requestData.SessionId, profile, completionService, userPrompt);
        }

        AIChatCompletionResponse completion = null;
        AIChatSessionPrompt message = null;
        AIChatCompletionChoice bestChoice = null;

        if (profile.Type == AIChatProfileType.TemplatePrompt)
        {
            completion = await completionService.ChatAsync([new ChatMessage(ChatRole.User, userPrompt)], new AIChatCompletionContext(profile)
            {
                UserMarkdownInResponse = true,
            });

            bestChoice = completion.Choices.FirstOrDefault();

            message = new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.Assistant,
                IsGeneratedPrompt = true,
                Title = profile.PromptSubject,
                Content = !string.IsNullOrEmpty(bestChoice?.Content)
                ? bestChoice.Content
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

            completion = await completionService.ChatAsync(transcript, new AIChatCompletionContext(profile)
            {
                Session = chatSession,
                UserMarkdownInResponse = requestData.IncludeHtmlResponse,
            });

            bestChoice = completion.Choices.FirstOrDefault();

            message = new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.Assistant,
                Title = profile.PromptSubject,
                Content = !string.IsNullOrEmpty(bestChoice?.Content)
                ? bestChoice.Content
                : AIConstants.DefaultBlankMessage,
            };
        }

        chatSession.Prompts.Add(message);

        await sessionManager.SaveAsync(chatSession);

        return TypedResults.Ok(new AIChatResponse
        {
            Success = completion.Choices.Any(),
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

    private static async Task<(AIChatSession ChatSession, bool IsNewSession)> GetSessionsAsync(IAIChatSessionManager sessionManager, IAIChatProfileManager profileManager, string sessionId, AIChatProfile profile, IAIChatCompletionService completionService, string userPrompt)
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
            var titleProfile = await profileManager.FindByNameAsync(AIConstants.GetTitleGeneratorProfileName(profile.Source));

            if (titleProfile is not null)
            {
                var transcription = new List<ChatMessage>
                {
                    new (ChatRole.User, userPrompt),
                };

                var context = new AIChatCompletionContext(titleProfile);

                if (string.IsNullOrEmpty(titleProfile.DeploymentId))
                {
                    context.DeploymentId = profile.DeploymentId;
                }

                var titleResponse = await completionService.ChatAsync(transcription, context);

                // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
                chatSession.Title = titleResponse.Choices.Any()
                    ? Str.Truncate(titleResponse.Choices.First().Content, 255)
                    : Str.Truncate(userPrompt, 255);
            }
        }

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Str.Truncate(userPrompt, 255);
        }

        return (chatSession, true);
    }

    private static async Task<IResult> GetToolMessageAsync(IAIChatCompletionService completionService, AIChatProfile profile, IAIMarkdownService markdownService, string prompt, bool respondWithHtml)
    {
        var completion = await completionService.ChatAsync([new ChatMessage(ChatRole.User, prompt)], new AIChatCompletionContext(profile)
        {
            UserMarkdownInResponse = true,
        });

        var bestChoice = completion.Choices.FirstOrDefault();

        return TypedResults.Ok(new AIChatResponse
        {
            Success = completion.Choices.Any(),
            Type = nameof(AIChatProfileType.Utility),
            Message = new AIChatResponseMessageDetailed
            {
                Content = bestChoice?.Content ?? AIConstants.DefaultBlankMessage,
                HtmlContent = respondWithHtml && !string.IsNullOrEmpty(bestChoice?.Content)
                ? markdownService.ToHtml(bestChoice.Content)
                : null,
            },
        });
    }

    private sealed class OpenAIChatCompletionRequest
    {
        public string SessionId { get; set; }

        public string ProfileId { get; set; }

        public string Prompt { get; set; }

        public string SessionProfileId { get; set; }

        public bool IncludeHtmlResponse { get; set; } = true;
    }
}
