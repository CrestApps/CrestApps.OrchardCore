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
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using YesSql;

namespace CrestApps.OrchardCore.AI.Hubs;

public class AIChatHub : Hub<IAIChatHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ISession _session;
    private readonly IAICompletionService _completionService;

    public AIChatHub(
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        ISession session,
        IAICompletionService completionService)
    {
        _authorizationService = authorizationService;
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _liquidTemplateManager = liquidTemplateManager;
        _session = session;
        _completionService = completionService;
    }

    public async Task SendMessage(string profileId, string prompt, string sessionId, string sessionProfileId = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError($"{nameof(profileId)} is required.");

            return;
        }

        var profile = await _profileManager.FindByIdAsync(profileId);

        if (profile is null)
        {
            await Clients.Caller.ReceiveError("Profile not found.");

            return;
        }

        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            await Clients.Caller.ReceiveError("You are not authorized to interact with the given profile.");

            return;
        }

        if (profile.Type == AIProfileType.Utility)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                await Clients.Caller.ReceiveError($"{nameof(prompt)} is required.");

                return;
            }

            await ProcessUtilityAsync(profile, prompt.Trim());

            // We don't need to save the session for utility profiles.
            return;
        }

        if (profile.Type == AIProfileType.TemplatePrompt)
        {
            if (string.IsNullOrWhiteSpace(sessionProfileId))
            {
                await Clients.Caller.ReceiveError($"{nameof(sessionProfileId)} is required.");

                return;
            }

            var parentProfile = await _profileManager.FindByIdAsync(sessionProfileId);

            if (parentProfile is null)
            {
                await Clients.Caller.ReceiveError($"Invalid value given to {nameof(sessionProfileId)}.");

                return;
            }

            await ProcessGeneratedPromptAsync(profile, sessionId, parentProfile);
        }
        else
        {
            // At this point, we are dealing with a chat profile.
            await ProcessChatPromptAsync(profile, sessionId, prompt.Trim());
        }

        await _session.SaveChangesAsync();
    }

    public async Task LoadSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await Clients.Caller.ReceiveError($"{nameof(sessionId)} is required.");

            return;
        }

        var chatSession = await _sessionManager.FindAsync(sessionId);

        if (chatSession == null)
        {
            await Clients.Caller.ReceiveError("Session not found.");

            return;
        }

        var profile = await _profileManager.FindByIdAsync(chatSession.ProfileId);

        if (profile is null)
        {
            await Clients.Caller.ReceiveError("Profile not found.");

            return;
        }

        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            await Clients.Caller.ReceiveError("You are not authorized to interact with the given profile.");

            return;
        }

        await Clients.Caller.LoadSession(new
        {
            chatSession.SessionId,
            Profile = new
            {
                Id = chatSession.ProfileId,
                Type = profile.Type.ToString()
            },
            Messages = chatSession.Prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.Id,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                References = message.References,
            })
        });
    }

    private async Task<(AIChatSession ChatSession, bool IsNewSession)> GetSessionsAsync(IAIChatSessionManager sessionManager, string sessionId, AIProfile profile, string userPrompt)
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

            var titleResponse = await _completionService.CompleteAsync(profile.Source,
            [
                new (ChatRole.User, userPrompt),
            ], context);

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

    private async Task ProcessChatPromptAsync(AIProfile profile, string sessionId, string prompt)
    {
        (var chatSession, var isNew) = await GetSessionsAsync(_sessionManager, sessionId, profile, prompt);

        chatSession.Prompts.Add(new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.User,
            Content = prompt,
        });

        var transcript = chatSession.Prompts
            .Where(x => !x.IsGeneratedPrompt)
            .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

        var assistantMessage = new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.Assistant,
            Title = profile.PromptSubject,
        };

        var builder = new StringBuilder();

        var completionContext = new AICompletionContext()
        {
            Profile = profile,
            Session = chatSession,
            UserMarkdownInResponse = true,
        };

        await Clients.Caller.StartMessageStream(assistantMessage.Id);

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, transcript, completionContext))
        {
            if (chunk.ChoiceIndex == 0)
            {
                if (chunk.AdditionalProperties is not null)
                {
                    if (chunk.AdditionalProperties.TryGetValue<IList<string>>("ContentItemIds", out var ids))
                    {
                        foreach (var id in ids)
                        {
                            contentItemIds.Add(id);
                        }
                    }

                    if (chunk.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
                    {
                        foreach (var (key, value) in referenceItems)
                        {
                            references[key] = value;
                        }
                    }
                }

                if (string.IsNullOrEmpty(chunk.Text))
                {
                    continue;
                }

                builder.Append(chunk.Text);

                var partialMessage = new CompletionPartialMessage
                {
                    Content = chunk.Text,
                    References = references,
                };

                await Clients.Caller.ReceiveMessageStream(partialMessage, assistantMessage.Id);
            }
        }

        await Clients.Caller.CompleteMessageStream(assistantMessage.Id);

        assistantMessage.Content = builder.ToString();
        assistantMessage.ContentItemIds = contentItemIds.ToList();
        assistantMessage.References = references;

        chatSession.Prompts.Add(assistantMessage);

        await _sessionManager.SaveAsync(chatSession);
    }

    private async Task ProcessGeneratedPromptAsync(AIProfile profile, string sessionId, AIProfile parentProfile)
    {
        (var chatSession, _) = await GetSessionsAsync(_sessionManager, sessionId, parentProfile, userPrompt: profile.Name);

        var generatedPrompt = await _liquidTemplateManager.RenderStringAsync(profile.PromptTemplate, NullEncoder.Default,
            new Dictionary<string, FluidValue>()
            {
                ["Profile"] = new ObjectValue(profile),
                ["Session"] = new ObjectValue(chatSession),
            });

        var assistantMessage = new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.Assistant,
            IsGeneratedPrompt = true,
            Title = profile.PromptSubject,
        };

        await Clients.Caller.StartMessageStream(assistantMessage.Id);

        var completionContext = new AICompletionContext()
        {
            Profile = profile,
            UserMarkdownInResponse = true,
        };

        var builder = new StringBuilder();

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, generatedPrompt)], completionContext))
        {
            if (chunk.ChoiceIndex == 0)
            {
                if (chunk.AdditionalProperties is not null)
                {
                    if (chunk.AdditionalProperties.TryGetValue<IList<string>>("ContentItemIds", out var ids))
                    {
                        foreach (var id in ids)
                        {
                            contentItemIds.Add(id);
                        }
                    }

                    if (chunk.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
                    {
                        foreach (var (key, value) in referenceItems)
                        {
                            references[key] = value;
                        }
                    }
                }

                if (string.IsNullOrEmpty(chunk.Text))
                {
                    continue;
                }

                builder.Append(chunk.Text);

                var partialMessage = new CompletionPartialMessage
                {
                    Content = chunk.Text,
                    References = references,
                };

                await Clients.Caller.ReceiveMessageStream(partialMessage, assistantMessage.Id);
            }
        }

        await Clients.Caller.CompleteMessageStream(assistantMessage.Id);

        assistantMessage.Content = builder.ToString();
        assistantMessage.ContentItemIds = contentItemIds.ToList();
        assistantMessage.References = references;

        chatSession.Prompts.Add(assistantMessage);

        await _sessionManager.SaveAsync(chatSession);
    }

    private async Task ProcessUtilityAsync(AIProfile profile, string prompt)
    {
        var messageId = IdGenerator.GenerateId();

        await Clients.Caller.StartMessageStream(messageId);

        var completionContext = new AICompletionContext
        {
            Profile = profile,
            UserMarkdownInResponse = true
        };

        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, prompt)], completionContext))
        {
            if (chunk.ChoiceIndex == 0)
            {
                if (chunk.AdditionalProperties is not null)
                {
                    if (chunk.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
                    {
                        foreach (var (key, value) in referenceItems)
                        {
                            references[key] = value;
                        }
                    }
                }

                if (string.IsNullOrEmpty(chunk.Text))
                {
                    continue;
                }

                var partialMessage = new CompletionPartialMessage
                {
                    Content = chunk.Text,
                    References = references,
                };

                await Clients.Caller.ReceiveMessageStream(partialMessage, messageId);
            }
        }

        await Clients.Caller.CompleteMessageStream(messageId);
    }
}
