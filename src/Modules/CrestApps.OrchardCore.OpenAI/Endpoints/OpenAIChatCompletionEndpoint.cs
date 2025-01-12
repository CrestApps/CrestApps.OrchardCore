using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Endpoints.Models;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.Support;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.OpenAI.Endpoints;

internal static class OpenAIChatCompletionEndpoint
{
    public static IEndpointRouteBuilder AddOpenAIChatCompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("OpenAI/ChatGPT/Completion", HandleAsync<T>)
            .AllowAnonymous()
            .WithName(OpenAIConstants.RouteNames.ChatCompletionRouteName)
            .DisableAntiforgery()
            .RequireCors(OpenAIConstants.Security.ExternalChatCORSPolicyName);

        return builder;
    }

    private static async Task<IResult> HandleAsync<T>(
        IAuthorizationService authorizationService,
        IOpenAIChatProfileManager chatProfileManager,
        IOpenAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IOpenAIMarkdownService markdownService,
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

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OpenAIChatPermissions.QueryAnyAIChatProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var completionService = serviceProvider.GetKeyedService<IOpenAIChatCompletionService>(profile.Source);

        if (completionService is null)
        {
            return TypedResults.Problem($"Unable to find a chat completion service for the source: '{profile.Source}'.");
        }

        string userPrompt;
        bool isNew;
        OpenAIChatSession chatSession;

        if (profile.Type == OpenAIChatProfileType.TemplatePrompt)
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

            if (profile.Type == OpenAIChatProfileType.Utility)
            {
                return await GetToolMessageAsync(completionService, profile, markdownService, userPrompt, requestData.IncludeHtmlResponse);
            }

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, requestData.SessionId, profile, completionService, userPrompt);
        }

        OpenAIChatCompletionResponse completion = null;
        OpenAIChatSessionPrompt message = null;
        OpenAIChatCompletionChoice bestChoice = null;

        if (profile.Type == OpenAIChatProfileType.TemplatePrompt)
        {
            completion = await completionService.ChatAsync([OpenAIChatCompletionMessage.CreateMessage(userPrompt, OpenAIConstants.Roles.User)], new OpenAIChatCompletionContext(profile)
            {
                SystemMessage = profile.SystemMessage,
                UserMarkdownInResponse = true,
            });

            bestChoice = completion.Choices.FirstOrDefault();

            message = new OpenAIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = OpenAIConstants.Roles.Assistant,
                IsGeneratedPrompt = true,
                Title = profile.PromptSubject,
                Content = !string.IsNullOrEmpty(bestChoice?.Content)
                ? bestChoice.Content
                : OpenAIConstants.DefaultBlankMessage,
            };
        }
        else
        {
            // At this point, we complete as standard chat.
            chatSession.Prompts.Add(new OpenAIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = OpenAIConstants.Roles.User,
                Content = userPrompt,
            });

            var transcript = chatSession.Prompts.Where(x => !x.IsGeneratedPrompt)
                .Select(x => OpenAIChatCompletionMessage.CreateMessage(x.Content, x.Role));

            completion = await completionService.ChatAsync(transcript, new OpenAIChatCompletionContext(profile)
            {
                SystemMessage = profile.SystemMessage,
                Session = chatSession,
                UserMarkdownInResponse = requestData.IncludeHtmlResponse,
            });

            bestChoice = completion.Choices.FirstOrDefault();

            message = new OpenAIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = OpenAIConstants.Roles.Assistant,
                Title = profile.PromptSubject,
                Content = !string.IsNullOrEmpty(bestChoice?.Content)
                ? bestChoice.Content
                : OpenAIConstants.DefaultBlankMessage,
            };
        }

        chatSession.Prompts.Add(message);

        await sessionManager.SaveAsync(chatSession);

        return TypedResults.Ok(new OpenAIChatResponse
        {
            Success = completion.Choices.Any(),
            Type = profile.Type.ToString(),
            SessionId = chatSession.SessionId,
            IsNew = isNew,
            Message = new OpenAIChatResponseMessageDetailed
            {
                Id = message.Id,
                Role = message.Role,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                HtmlContent = requestData.IncludeHtmlResponse && !string.IsNullOrEmpty(message.Content)
                ? markdownService.ToHtml(message.Content)
                : null,
            },
        });
    }

    private static async Task<(OpenAIChatSession ChatSession, bool IsNewSession)> GetSessionsAsync(IOpenAIChatSessionManager sessionManager, string sessionId, OpenAIChatProfile profile, IOpenAIChatCompletionService completionService, string userPrompt)
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

        if (profile.TitleType == OpenAISessionTitleType.Generated)
        {
            var titleResponse = await completionService.GetTitleAsync(userPrompt, profile);

            // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
            chatSession.Title = titleResponse.Choices.Any()
                ? Str.Truncate(titleResponse.Choices.First().Content, 255)
                : Str.Truncate(userPrompt, 255);
        }
        else
        {
            chatSession.Title = userPrompt;
        }

        return (chatSession, true);
    }

    private static async Task<IResult> GetToolMessageAsync(IOpenAIChatCompletionService completionService, OpenAIChatProfile profile, IOpenAIMarkdownService markdownService, string prompt, bool respondWithHtml)
    {
        var completion = await completionService.ChatAsync([OpenAIChatCompletionMessage.CreateMessage(prompt, OpenAIConstants.Roles.User)], new OpenAIChatCompletionContext(profile)
        {
            SystemMessage = profile.SystemMessage,
            UserMarkdownInResponse = true,
        });

        var bestChoice = completion.Choices.FirstOrDefault();

        return TypedResults.Ok(new OpenAIChatResponse
        {
            Success = completion.Choices.Any(),
            Type = nameof(OpenAIChatProfileType.Utility),
            Message = new OpenAIChatResponseMessageDetailed
            {
                Content = bestChoice?.Content ?? OpenAIConstants.DefaultBlankMessage,
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
