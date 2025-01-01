using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Models;
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
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using OrchardCore.Markdown.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Endpoints;

internal static class OpenAIChatCompletionEndpoint
{
    private const string _blankMessage = "AI drew blank and no message was generated!";

    public static IEndpointRouteBuilder AddOpenAIChatCompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("OpenAI/ChatGPT/Completion", HandleAsync<T>)
            .AllowAnonymous()
            .WithName(OpenAIConstants.RouteNames.ChatCompletionRouteName)
            .DisableAntiforgery()
            .RequireCors(OpenAIConstants.Security.ExternalWidgetsCORSPolicyName);

        return builder;
    }

    private static async Task<IResult> HandleAsync<T>(
        IAuthorizationService authorizationService,
        IOpenAIChatProfileManager chatProfileManager,
        IOpenAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IEnumerable<IOpenAIChatEventHandler> handlers,
        IMarkdownService markdownService,
        ILogger<T> logger,
        ChatRequest requestData)
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

        if (string.IsNullOrWhiteSpace(requestData.Prompt))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>()
            {
                { nameof(requestData.Prompt), ["Prompt is required"] },
            });
        }

        var completionService = serviceProvider.GetKeyedService<IOpenAIChatCompletionService>(profile.Source);

        if (completionService is null)
        {
            return TypedResults.Problem($"Unable to find a chat completion service for the source: '{profile.Source}'.");
        }

        if (profile.Type == OpenAIChatProfileType.Tool)
        {
            return await GetToolMessageAsync(completionService, profile, markdownService, requestData.Prompt);
        }

        string clientId = null;
        string userId = null;
        OpenAIChatSession chatSession = null;

        if (!string.IsNullOrWhiteSpace(requestData.SessionId))
        {
            chatSession = await sessionManager.FindAsync(requestData.SessionId, profile.Id);
        }

        var trimmedPrompt = requestData.Prompt.Trim();
        var isNew = false;

        if (chatSession == null)
        {
            // At this point, we need to create a new session.
            isNew = true;
            chatSession = await sessionManager.NewAsync(profile);

            if (profile.TitleType == OpenAISessionTitleType.Generated)
            {
                var titleResponse = await completionService.GetTitleAsync(trimmedPrompt, profile);

                // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
                chatSession.Title = titleResponse.Choices.Any()
                    ? Str.Truncate(titleResponse.Choices.First().Message, 255)
                    : Str.Truncate(trimmedPrompt, 255);
            }
            else
            {
                chatSession.Title = trimmedPrompt;
            }
        }

        var part = chatSession.As<OpenAIChatSessionPart>();

        OpenAIChatCompletionResponse completion = null;
        OpenAIChatSessionMessage message = null;
        OpenAIChatCompletionChoice bestChoice = null;

        if (profile.Type == OpenAIChatProfileType.GeneratedPrompt)
        {
            var prompt = await liquidTemplateManager.RenderStringAsync(profile.PromptTemplate, NullEncoder.Default,
                new Dictionary<string, FluidValue>()
                {
                    ["Session"] = new ObjectValue(chatSession),
                });

            completion = await completionService.ChatAsync([OpenAIChatCompletionMessage.CreateMessage(prompt, OpenAIConstants.Roles.User)], new OpenAIChatCompletionContext(profile)
            {
                SystemMessage = profile.SystemMessage,
                UserMarkdownInResponse = true,
            });

            bestChoice = completion.Choices.FirstOrDefault();

            message = new OpenAIChatSessionMessage
            {
                Id = IdGenerator.GenerateId(),
                Role = OpenAIConstants.Roles.Assistant,
                IsGeneratedPrompt = true,
                Prompt = !string.IsNullOrEmpty(bestChoice?.Message)
                ? bestChoice.Message
                : _blankMessage,
            };
        }
        else
        {
            // At this point, we complete as standard chat.
            part.Prompts.Add(new OpenAIChatSessionMessage
            {
                Id = IdGenerator.GenerateId(),
                Role = OpenAIConstants.Roles.User,
                Prompt = trimmedPrompt,
            });

            var transcript = part.Prompts.Where(x => !x.IsGeneratedPrompt)
                .Select(x => OpenAIChatCompletionMessage.CreateMessage(x.Prompt, x.Role));

            completion = await completionService.ChatAsync(transcript, new OpenAIChatCompletionContext(profile)
            {
                SystemMessage = profile.SystemMessage,
                Session = chatSession,
                UserMarkdownInResponse = true,
            });

            bestChoice = completion.Choices.FirstOrDefault();

            message = new OpenAIChatSessionMessage
            {
                Id = IdGenerator.GenerateId(),
                Role = OpenAIConstants.Roles.Assistant,
                Prompt = !string.IsNullOrEmpty(bestChoice?.Message)
                ? bestChoice.Message
                : _blankMessage,
            };
        }

        var completedChatContext = new OpenAICompletedChatContext
        {
            Profile = profile,
            SessionId = chatSession.SessionId,
            Prompt = requestData.Prompt,
            TotalHits = bestChoice?.ContentItemIds?.Count ?? 0,
            UserId = userId,
            ClientId = clientId,
            ContentItemIds = bestChoice?.ContentItemIds ?? [],
            MessageId = message.Id,
        };

        await handlers.InvokeAsync((handler, ctx) => handler.CompletedAsync(ctx), completedChatContext, logger);

        part.Prompts.Add(message);

        chatSession.Put(part);

        await sessionManager.SaveAsync(chatSession);

        return TypedResults.Ok(new
        {
            Success = completion.Choices.Any(),
            Type = profile.Type.ToString(),
            chatSession.SessionId,
            IsNew = isNew,
            Message = new
            {
                message.Id,
                message.Role,
                message.IsGeneratedPrompt,
                message.Prompt,
                PromptHTML = !string.IsNullOrEmpty(message.Prompt)
                ? markdownService.ToHtml(message.Prompt)
                : null,
            },
        });
    }

    private static async Task<IResult> GetToolMessageAsync(IOpenAIChatCompletionService completionService, OpenAIChatProfile profile, IMarkdownService markdownService, string prompt)
    {
        var completion = await completionService.ChatAsync([OpenAIChatCompletionMessage.CreateMessage(prompt, OpenAIConstants.Roles.User)], new OpenAIChatCompletionContext(profile)
        {
            SystemMessage = profile.SystemMessage,
            UserMarkdownInResponse = true,
        });

        var bestChoice = completion.Choices.FirstOrDefault();

        return TypedResults.Ok(new
        {
            Success = completion.Choices.Any(),
            Type = "Tool",
            Message = new
            {
                Prompt = bestChoice?.Message ?? _blankMessage,
                PromptHTML = !string.IsNullOrEmpty(bestChoice?.Message)
                ? markdownService.ToHtml(bestChoice.Message)
                : _blankMessage,
            },
        });
    }

    internal sealed class ChatRequest
    {
        public string SessionId { get; set; }

        public string ProfileId { get; set; }

        public string Prompt { get; set; }
    }
}
