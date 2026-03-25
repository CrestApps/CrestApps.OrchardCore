using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Endpoints.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.Support;
using Cysharp.Text;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
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
       [FromServices] IAuthorizationService authorizationService,
       [FromServices] INamedCatalogManager<AIProfile> chatProfileManager,
       [FromServices] IAIChatSessionManager sessionManager,
       [FromServices] IAIChatSessionPromptStore promptStore,
       [FromServices] ILiquidTemplateManager liquidTemplateManager,
       [FromServices] IHttpContextAccessor httpContextAccessor,
       [FromServices] IAICompletionService completionService,
       [FromServices] IAICompletionContextBuilder completionContextBuilder,
       [FromServices] IOrchestrationContextBuilder orchestrationContextBuilder,
       [FromServices] IOrchestratorResolver orchestratorResolver,
       [FromServices] CitationReferenceCollector citationCollector,
       [FromServices] IAITemplateService aiTemplateService,
       [FromServices] IAIDeploymentManager deploymentManager,
       [FromServices] ILogger<T> logger,
       [FromBody] AICompletionRequest requestData)
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

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, requestData.SessionId, parentProfile, completionService, userPrompt: profile.Name, completionContextBuilder, aiTemplateService, deploymentManager);

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
                return await GetUtilityMessageAsync(completionService, profile, userPrompt, completionContextBuilder, deploymentManager);
            }

            (chatSession, isNew) = await GetSessionsAsync(sessionManager, requestData.SessionId, profile, completionService, userPrompt, completionContextBuilder, aiTemplateService, deploymentManager);
        }

        if (!isNew &&
            !string.IsNullOrWhiteSpace(userPrompt) &&
            (string.IsNullOrWhiteSpace(chatSession.Title) || chatSession.Title == AIConstants.DefaultBlankSessionTitle))
        {
            var titleUserPrompt = BuildTitleUserPrompt(profile, userPrompt);
            if (profile.TitleType == AISessionTitleType.Generated)
            {
                chatSession.Title = await GetGeneratedTitleAsync(profile, titleUserPrompt, completionService, completionContextBuilder, aiTemplateService, deploymentManager);
            }

            if (string.IsNullOrWhiteSpace(chatSession.Title) || chatSession.Title == AIConstants.DefaultBlankSessionTitle)
            {
                chatSession.Title = Str.Truncate(titleUserPrompt, 255);
            }
        }

        AIChatSessionPrompt message;

        using var invocationScope = AIInvocationScope.Begin();

        if (profile.Type == AIProfileType.TemplatePrompt)
        {
            var contextForTemplate = await completionContextBuilder.BuildAsync(profile);
            var templateDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentId: contextForTemplate.ChatDeploymentId)
                ?? throw new InvalidOperationException("Unable to resolve a chat deployment for the profile.");

            var completion = await completionService.CompleteAsync(templateDeployment, [new ChatMessage(ChatRole.User, userPrompt)], contextForTemplate);

            var bestChoice = completion?.Messages?.FirstOrDefault();

            message = new AIChatSessionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                SessionId = chatSession.SessionId,
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
            var userPromptRecord = new AIChatSessionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.User,
                Content = userPrompt,
            };

            await promptStore.CreateAsync(userPromptRecord);

            var existingPrompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

            var transcript = existingPrompts.Where(x => !x.IsGeneratedPrompt)
                .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

            // Build the orchestration context using the handler pipeline (same as the hubs).
            var orchestratorContext = await orchestrationContextBuilder.BuildAsync(profile, ctx =>
            {
                ctx.UserMessage = userPrompt;
                ctx.ConversationHistory = transcript.ToList();
                ctx.CompletionContext.AdditionalProperties["Session"] = chatSession;
            });

            // Store the session in the invocation context so document tools can resolve session documents.
            AIInvocationScope.Current.Items[nameof(AIChatSession)] = chatSession;
            AIInvocationScope.Current.DataSourceId = orchestratorContext.CompletionContext.DataSourceId;

            // Resolve the orchestrator for this profile and execute the completion.
            var orchestrator = orchestratorResolver.Resolve(profile.OrchestratorName);

            var contentItemIds = new HashSet<string>();
            var references = new Dictionary<string, AICompletionReference>();
            var builder = ZString.CreateStringBuilder();

            // Collect preemptive RAG references.
            citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);

            await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestratorContext))
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    builder.Append(chunk.Text);
                }

                // Incrementally collect any new tool references.
                citationCollector.CollectToolReferences(references, contentItemIds);
            }

            // Final pass to collect any tool references added by the last tool call.
            citationCollector.CollectToolReferences(references, contentItemIds);

            message = new AIChatSessionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.Assistant,
                Title = profile.PromptSubject,
                Content = builder.Length > 0
                    ? builder.ToString()
                    : AIConstants.DefaultBlankMessage,
                ContentItemIds = contentItemIds.ToList(),
                References = references,
            };
        }

        await promptStore.CreateAsync(message);

        await sessionManager.SaveAsync(chatSession);

        return TypedResults.Ok(new AIChatResponse
        {
            Success = !string.IsNullOrEmpty(message.Content) && message.Content != AIConstants.DefaultBlankMessage,
            Type = profile.Type.ToString(),
            SessionId = chatSession.SessionId,
            IsNew = isNew,
            Message = new AIChatResponseMessageDetailed
            {
                Id = message.ItemId,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                References = message.References,
                Appearance = message.As<AssistantMessageAppearance>(),
            },
        });
    }

    private static async Task<(AIChatSession ChatSession, bool IsNewSession)> GetSessionsAsync(IAIChatSessionManager sessionManager, string sessionId, AIProfile profile, IAICompletionService completionService, string userPrompt, IAICompletionContextBuilder completionContextBuilder, IAITemplateService aiTemplateService, IAIDeploymentManager deploymentManager)
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
        var titleUserPrompt = BuildTitleUserPrompt(profile, userPrompt);

        if (profile.TitleType == AISessionTitleType.Generated)
        {
            // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
            chatSession.Title = await GetGeneratedTitleAsync(profile, titleUserPrompt, completionService, completionContextBuilder, aiTemplateService, deploymentManager);
        }

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Str.Truncate(titleUserPrompt, 255);
        }

        return (chatSession, true);
    }

    private static async Task<string> GetGeneratedTitleAsync(
        AIProfile profile,
        string userPrompt,
        IAICompletionService completionService,
        IAICompletionContextBuilder completionContextBuilder,
        IAITemplateService aiTemplateService,
        IAIDeploymentManager deploymentManager)
    {
        var titleSystemMessage = await aiTemplateService.RenderAsync(AITemplateIds.TitleGeneration);

        var context = await completionContextBuilder.BuildAsync(profile, c =>
        {
            c.SystemMessage = titleSystemMessage;
            c.FrequencyPenalty = 0;
            c.PresencePenalty = 0;
            c.TopP = 1;
            c.Temperature = 0;
            c.MaxTokens = 64; // 64 token to generate about 255 characters.

            // Avoid using tools or any data sources when generating title to reduce the used tokens.
            c.DataSourceId = null;
            c.DisableTools = true;
        });

        // Prefer utility deployment for title generation, fall back to chat.
        var deployment = await deploymentManager.ResolveUtilityOrDefaultAsync(
            utilityDeploymentId: context.UtilityDeploymentId,
            chatDeploymentId: context.ChatDeploymentId);

        if (deployment == null)
        {
            return Str.Truncate(userPrompt, 255);
        }

        var titleResponse = await completionService.CompleteAsync(
        deployment,
        [
            new (ChatRole.User, userPrompt),
        ], context);

        // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
        return titleResponse.Messages.Count > 0
            ? Str.Truncate(titleResponse.Messages.First().Text, 255)
            : Str.Truncate(userPrompt, 255);
    }

    private static async Task<IResult> GetUtilityMessageAsync(IAICompletionService completionService, AIProfile profile, string prompt, IAICompletionContextBuilder completionContextBuilder, IAIDeploymentManager deploymentManager)
    {
        var context = await completionContextBuilder.BuildAsync(profile);
        var deployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentId: context.ChatDeploymentId)
            ?? throw new InvalidOperationException("Unable to resolve a chat deployment for the profile.");

        var completion = await completionService.CompleteAsync(deployment, [new ChatMessage(ChatRole.User, prompt)], context);

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

    private static string BuildTitleUserPrompt(AIProfile profile, string userPrompt)
    {
        var trimmedUserPrompt = userPrompt?.Trim();
        var profileMetadata = profile.As<AIProfileMetadata>();
        var initialPrompt = profileMetadata.InitialPrompt?.Trim();

        if (string.IsNullOrWhiteSpace(initialPrompt))
        {
            return trimmedUserPrompt;
        }

        return string.IsNullOrWhiteSpace(trimmedUserPrompt)
            ? initialPrompt
            : $"{initialPrompt}\n\n{trimmedUserPrompt}";
    }
}
