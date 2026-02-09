using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Endpoints.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.Support;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

internal static class ApiAICompletionEndpoint
{
    public static IEndpointRouteBuilder AddApiAICompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("api/ai/completion/chat", HandleAsync<T>)
            .DisableAntiforgery()
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Api" });

        return builder;
    }

    private static async Task<IResult> HandleAsync<T>(
       IAuthorizationService authorizationService,
       INamedCatalogManager<AIProfile> chatProfileManager,
       IAIChatSessionManager sessionManager,
       ILiquidTemplateManager liquidTemplateManager,
       IHttpContextAccessor httpContextAccessor,
       IAICompletionService completionService,
       IAICompletionContextBuilder completionContextBuilder,
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

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, requestData.SessionId, parentProfile, completionService, userPrompt: profile.Name, completionContextBuilder);

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
                return await GetUtilityMessageAsync(completionService, profile, userPrompt, completionContextBuilder);
            }

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, requestData.SessionId, profile, completionService, userPrompt, completionContextBuilder);
        }

        ChatResponse completion = null;
        AIChatSessionPrompt message = null;
        ChatMessage bestChoice = null;

        if (profile.Type == AIProfileType.TemplatePrompt)
        {
            var contextForTemplate = await completionContextBuilder.BuildAsync(profile);
            completion = await completionService.CompleteAsync(profile.Source, [new ChatMessage(ChatRole.User, userPrompt)], contextForTemplate);

            bestChoice = completion?.Messages?.FirstOrDefault();

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

            var context = await completionContextBuilder.BuildAsync(profile, c =>
            {
                c.AdditionalProperties["Session"] = chatSession;
            });

            completion = await completionService.CompleteAsync(profile.Source, transcript, context);

            bestChoice = completion?.Messages?.FirstOrDefault();

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

        if (completion.AdditionalProperties is not null)
        {
            if (completion.AdditionalProperties.TryGetValue<IList<string>>("ContentItemIds", out var contentItemIds))
            {
                message.ContentItemIds = contentItemIds;
            }

            if (completion.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var references))
            {
                message.References = references;
            }
        }

        chatSession.Prompts.Add(message);

        await sessionManager.SaveAsync(chatSession);

        return TypedResults.Ok(new AIChatResponse
        {
            Success = completion?.Messages?.Any() ?? false,
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
                References = message.References,
            },
        });
    }

    private static async Task<(AIChatSession ChatSession, bool IsNewSession)> GetSessionsAsync(IAIChatSessionManager sessionManager, string sessionId, AIProfile profile, IAICompletionService completionService, string userPrompt, IAICompletionContextBuilder completionContextBuilder)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existingChatSession = await sessionManager.FindAsync(sessionId);

            if (existingChatSession != null && existingChatSession.ProfileId == profile.ItemId)
            {
                return (existingChatSession, false);
            }
        }

        // At this point, we need to create a new session.
        var chatSession = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());

        if (profile.TitleType == AISessionTitleType.Generated)
        {
            // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
            chatSession.Title = await GetGeneratedTitleAsync(profile, userPrompt, completionService, completionContextBuilder);
        }

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Str.Truncate(userPrompt, 255);
        }

        return (chatSession, true);
    }

    private static async Task<string> GetGeneratedTitleAsync(
        AIProfile profile,
        string userPrompt,
        IAICompletionService completionService,
       IAICompletionContextBuilder completionContextBuilder)
    {
        var context = await completionContextBuilder.BuildAsync(profile, c =>
        {
            c.SystemMessage = AIConstants.TitleGeneratorSystemMessage;
            c.FrequencyPenalty = 0;
            c.PresencePenalty = 0;
            c.TopP = 1;
            c.Temperature = 0;
            c.MaxTokens = 64; // 64 token to generate about 255 characters.
            c.UserMarkdownInResponse = false;

            // Avoid using tools or any data sources when generating title to reduce the used tokens.
            c.DataSourceId = null;
            c.DataSourceType = null;
            c.DisableTools = true;
        });

        var titleResponse = await completionService.CompleteAsync(profile.Source,
        [
            new (ChatRole.User, userPrompt),
        ], context);

        // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
        return titleResponse.Messages.Count > 0
            ? Str.Truncate(titleResponse.Messages.First().Text, 255)
            : Str.Truncate(userPrompt, 255);
    }

    private static async Task<IResult> GetUtilityMessageAsync(IAICompletionService completionService, AIProfile profile, string prompt, IAICompletionContextBuilder completionContextBuilder)
    {
        var context = await completionContextBuilder.BuildAsync(profile);
        var completion = await completionService.CompleteAsync(profile.Source, [new ChatMessage(ChatRole.User, prompt)], context);

        var result = new AIChatResponse
        {
            Success = completion.Messages.Count > 0,
            Type = nameof(AIProfileType.Utility),
            Message = new AIChatResponseMessageDetailed(),
        };

        if (completion.AdditionalProperties is not null)
        {
            if (completion.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
            {
                result.Message.References = referenceItems;
            }
        }

        result.Message.Content = completion.Messages.FirstOrDefault()?.Text ?? AIConstants.DefaultBlankMessage;

        return TypedResults.Ok(result);
    }
}
