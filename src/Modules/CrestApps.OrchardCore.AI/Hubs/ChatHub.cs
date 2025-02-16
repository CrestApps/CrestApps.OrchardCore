using System.Text;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Endpoints.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using YesSql;

namespace CrestApps.OrchardCore.AI.Hubs;

public class ChatHub : Hub
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIProfileManager _chatProfileManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;

    public ChatHub(
        IAuthorizationService authorizationService,
        IAIProfileManager chatProfileManager,
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        ISession session,
        IServiceProvider serviceProvider)
    {
        _authorizationService = authorizationService;
        _chatProfileManager = chatProfileManager;
        _sessionManager = sessionManager;
        _liquidTemplateManager = liquidTemplateManager;
        _session = session;
        _serviceProvider = serviceProvider;
    }

    public async Task SendMessage(string profileId, string prompt, string sessionId, string sessionProfileId = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.SendAsync("ReceiveError", "ProfileId is required.");
            return;
        }

        var profile = await _chatProfileManager.FindByIdAsync(profileId);

        if (profile is null)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Profile not found.");
            return;
        }

        var httpContext = Context.GetHttpContext();
        var user = httpContext.User;

        if (!await _authorizationService.AuthorizeAsync(user, AIPermissions.QueryAnyAIProfile, profile))
        {
            await Clients.Caller.SendAsync("ReceiveError", "Unauthorized.");
            return;
        }

        var completionService = _serviceProvider.GetKeyedService<IAICompletionService>(profile.Source);

        if (completionService is null)
        {
            await Clients.Caller.SendAsync("ReceiveError", $"Unable to find a chat completion service for the source: '{profile.Source}'.");
            return;
        }

        string userPrompt;
        bool isNew;
        AIChatSession chatSession;

        if (profile.Type == AIProfileType.TemplatePrompt)
        {
            if (string.IsNullOrWhiteSpace(sessionProfileId))
            {
                await Clients.Caller.SendAsync("ReceiveError", "SessionProfileId is required.");
                return;
            }

            var parentProfile = await _chatProfileManager.FindByIdAsync(sessionProfileId);

            if (parentProfile is null)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Parent profile not found.");
                return;
            }

            (chatSession, isNew) = await GetSessionsAsync(_sessionManager, sessionId, parentProfile, completionService, userPrompt: profile.Name);

            userPrompt = await _liquidTemplateManager.RenderStringAsync(profile.PromptTemplate, NullEncoder.Default,
                new Dictionary<string, FluidValue>
                {
                    ["Profile"] = new ObjectValue(profile),
                    ["Session"] = new ObjectValue(chatSession)
                });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                await Clients.Caller.SendAsync("ReceiveError", "Prompt is required.");
                return;
            }

            userPrompt = prompt.Trim();

            if (profile.Type == AIProfileType.Utility)
            {
                var response = await GetToolMessageAsync(completionService, profile, userPrompt);
                await Clients.Caller.SendAsync("ReceiveMessage", response);
                return;
            }

            (chatSession, isNew) = await GetSessionsAsync(_sessionManager, sessionId, profile, completionService, userPrompt: userPrompt);
        }

        var transcript = chatSession.Prompts
            .Where(x => !x.IsGeneratedPrompt)
            .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

        var completionContext = new AICompletionContext
        {
            Profile = profile,
            Session = chatSession,
            UserMarkdownInResponse = false
        };

        var builder = new StringBuilder();

        await foreach (var chunk in completionService.CompleteStreamingAsync(transcript, completionContext))
        {
            if (chunk.ChoiceIndex == 0)
            {
                builder.Append(chunk.Text);

                await Clients.Caller.SendAsync("ReceiveMessage", chunk.Text);
            }
        }

        var message = new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.Assistant,
            Title = profile.PromptSubject,
            Content = builder.ToString(),
        };

        chatSession.Prompts.Add(message);
        await _sessionManager.SaveAsync(chatSession);

        var responseMessage = new AIChatResponseMessageDetailed
        {
            Id = message.Id,
            Role = message.Role.Value,
            IsGeneratedPrompt = message.IsGeneratedPrompt,
            Title = message.Title,
            Content = message.Content,
            HtmlContent = null,
        };

        await _session.SaveChangesAsync();
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

    private async static Task<AIChatResponse> GetToolMessageAsync(IAICompletionService completionService, AIProfile profile, string prompt)
    {
        var completion = await completionService.CompleteAsync([new ChatMessage(ChatRole.User, prompt)], new AICompletionContext
        {
            Profile = profile,
            UserMarkdownInResponse = true
        });

        var bestChoice = completion.Choices.FirstOrDefault();

        return new AIChatResponse
        {
            Success = completion.Choices.Any(),
            Type = nameof(AIProfileType.Utility),
            Message = new AIChatResponseMessageDetailed
            {
                Content = bestChoice?.Text ?? AIConstants.DefaultBlankMessage,
                HtmlContent = null
            }
        };
    }
}
